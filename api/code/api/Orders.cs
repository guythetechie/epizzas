using common;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace api;

internal static class OrdersModule
{
    public static void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        CommonModule.ConfigureGetCurrentTime(builder);
        CosmosModule.ConfigureCancelCosmosOrder(builder);
        CosmosModule.ConfigureCreateCosmosOrder(builder);
        CosmosModule.ConfigureFindCosmosOrder(builder);
        CosmosModule.ConfigureListCosmosOrders(builder);
    }

    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/v1/orders");

        group.MapDelete("/{orderId}", OrderHandlers.Cancel);
        group.MapPost("/", OrderHandlers.Create);
        group.MapGet("/{orderId}", OrderHandlers.GetById);
        group.MapGet("/", OrderHandlers.List);
    }
}

internal static class OrderHandlers
{
    public static async ValueTask<IResult> Cancel(string orderId,
                                                  [FromServices] CancelCosmosOrder cancelCosmosOrder,
                                                  [FromHeader(Name = "If-Match")] string? ifMatch,
                                                  CancellationToken cancellationToken)
    {
        var operation =
            from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
            from eTag in ApiOperation.Lift(getETag(ifMatch))
            from _ in ApiOperation.Lift(cancelOrder(validatedOrderId, eTag, cancelCosmosOrder, cancellationToken))
            select getSuccessfulResponse();

        return await operation.Run(cancellationToken);

        static Either<IResult, OrderId> validateOrderId(string orderId) =>
            OrderId.From(orderId)
                   .ToEither(error => Results.BadRequest(new
                   {
                       code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                       message = error.Message
                   }));

        static Either<IResult, ETag> getETag(string? ifMatch) =>
            ETag.From(ifMatch)
                .ToEither(error => Results.BadRequest(new
                {
                    code = ApiErrorCode.InvalidRequestHeader.Instance.ToString(),
                    message = "If-Match header is required.",
                }));

        static async ValueTask<Either<IResult, Unit>> cancelOrder(OrderId orderId,
                                                                  ETag eTag,
                                                                  CancelCosmosOrder cancelCosmosOrder,
                                                                  CancellationToken cancellationToken)
        {
            var result = await cancelCosmosOrder(orderId, eTag, cancellationToken);

            return result.Match(error => error switch
            {
                CosmosError.NotFound => Either<IResult, Unit>.Right(Unit.Default),
                CosmosError.ETagMismatch => Either<IResult, Unit>.Left(Results.Json(new
                {
                    code = ApiErrorCode.ETagMismatch.Instance.ToString(),
                    message = $"Could not cancel order '{orderId}'. Another process might have modified the resource. Please try again.",
                },
                    statusCode: (int)HttpStatusCode.PreconditionFailed
                )),
                _ => throw error.ToException()
            },
            unit => unit);
        }

        static IResult getSuccessfulResponse() => Results.NoContent();
    }

    public static async ValueTask<IResult> Create([FromServices] CreateCosmosOrder createCosmosOrder,
                                                  [FromServices] GetCurrentTime getCurrentTime,
                                                  Stream? body,
                                                  CancellationToken cancellationToken)
    {
        var operation =
            from order in ApiOperation.Lift(parseOrder(body, getCurrentTime, cancellationToken))
            from _ in ApiOperation.Lift(createOrder(createCosmosOrder, order, cancellationToken))
            select getSuccessfulResponse();

        return await operation.Run(cancellationToken);

        static async ValueTask<Either<IResult, Order>> parseOrder(Stream? body,
                                                                  GetCurrentTime getCurrentTime,
                                                                  CancellationToken cancellationToken)
        {
            var result = from jsonNode in await JsonNodeModule.From(body, cancellationToken: cancellationToken)
                         from jsonObject in jsonNode.AsJsonObject()
                         let status = new OrderStatus.Created
                         {
                             Date = getCurrentTime(),
                             By = "system"
                         }
                         let statusJson = OrderStatus.Serialize(status)
                         let orderJson = jsonObject.SetProperty("status", statusJson)
                         from order in Order.Deserialize(orderJson)
                         select order;

            return result.ToEither(error => Results.BadRequest(new
            {
                code = ApiErrorCode.InvalidRequestBody.Instance.ToString(),
                message = error.Message,
                details = (string[])[.. error.Details.Select(error => error.Message)]
            }));
        }

        static async ValueTask<Either<IResult, Unit>> createOrder(CreateCosmosOrder createCosmosOrder,
                                                                  Order order,
                                                                  CancellationToken cancellationToken)
        {
            var result = await createCosmosOrder(order, cancellationToken);

            return result.MapLeft(error => error switch
            {
                CosmosError.AlreadyExists => Results.Conflict(new
                {
                    code = ApiErrorCode.ResourceAlreadyExists.Instance.ToString(),
                    message = $"Order with ID {order.Id} already exists.",
                }),
                _ => throw error.ToException()
            });
        }

        static IResult getSuccessfulResponse() => Results.NoContent();
    }

    public static async ValueTask<IResult> GetById(string orderId,
                                                   [FromServices] FindCosmosOrder findCosmosOrder,
                                                   CancellationToken cancellationToken)
    {
        var operation =
            from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
            from orderAndETag in ApiOperation.Lift(getOrder(validatedOrderId, findCosmosOrder, cancellationToken))
            let successfulResponse = getSuccessfulResponse(orderAndETag.Item1, orderAndETag.Item2)
            select successfulResponse;

        return await operation.Run(cancellationToken);

        static Either<IResult, OrderId> validateOrderId(string orderId) =>
            OrderId.From(orderId)
                   .ToEither(error => Results.BadRequest(new
                   {
                       code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                       message = error.Message
                   }));

        static async ValueTask<Either<IResult, (Order, ETag)>> getOrder(OrderId orderId,
                                                                        FindCosmosOrder findCosmosOrder,
                                                                        CancellationToken cancellationToken)
        {
            var option = await findCosmosOrder(orderId, cancellationToken);

            return option.ToEither(() => Results.NotFound(new
            {
                code = ApiErrorCode.ResourceNotFound.Instance.ToString(),
                message = $"Order with ID {orderId} was not found."
            }));
        }

        static IResult getSuccessfulResponse(Order order, ETag eTag) =>
            Results.Ok(new JsonObject
            {
                ["eTag"] = eTag.ToString(),
                ["status"] = OrderStatus.Serialize(order.Status),
                ["pizzas"] = order.Pizzas.Select(Pizza.Serialize).ToJsonArray(),
            });
    }

    public static async ValueTask<IResult> List([FromServices] ListCosmosOrders listCosmosOrders,
                                                CancellationToken cancellationToken)
    {
        var operation = from orders in ApiOperation.Lift(listCosmosOrders(cancellationToken))
                        select getSuccessfulResponse(orders);

        return await operation.Run(cancellationToken);

        static IResult getSuccessfulResponse(IEnumerable<(Order, ETag)> orders) =>
            Results.Ok(new JsonObject
            {
                ["values"] = orders.Select(order => Order.Serialize(order.Item1)
                                                         .SetProperty("eTag", order.Item2.ToString()))
                                   .ToJsonArray(),
            });
    }
}