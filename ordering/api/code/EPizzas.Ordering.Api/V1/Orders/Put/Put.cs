using CommunityToolkit.Diagnostics;
using EPizzas.Common;
using Flurl;
using LanguageExt;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api.V1.Orders.Put;

public record RequestModel
{
    private readonly Seq<Pizza> pizzas;

    public IReadOnlyList<Pizza> Pizzas
    {
        get => pizzas.Freeze();
        init => pizzas = ValidatePizzas(value).ToSeq();
    }

    private static IReadOnlyList<Pizza> ValidatePizzas(IReadOnlyList<Pizza> pizzas)
    {
        Guard.IsNotEmpty(pizzas.Freeze(), nameof(pizzas));
        return pizzas;
    }
}

public abstract record CreateError
{
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    public sealed record ResourceAlreadyExists : CreateError;
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
}

public abstract record UpdateError
{
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    public sealed record ResourceDoesNotExist : UpdateError;
    public sealed record ETagMismatch : UpdateError;
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
}

public delegate ValueTask<Option<Order>> FindOrder(OrderId orderId, CancellationToken cancellationToken);

public delegate ValueTask<Either<CreateError, ETag>> CreateOrder(Order order, CancellationToken cancellationToken);

public delegate ValueTask<Either<UpdateError, ETag>> UpdateOrder(Order order, ETag ifMatchETag, CancellationToken cancellationToken);

internal static class Endpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapPut("/{orderId}", Handler.Handle);
    }
}


internal static class Handler
{
    public static async ValueTask<IResult> Handle(string orderId,
                                                  [FromBody] JsonNode? body,
                                                  HttpRequest request,
                                                  [FromServices] CreateOrder createOrder,
                                                  [FromServices] FindOrder findOrder,
                                                  [FromServices] UpdateOrder updateOrder,
                                                  CancellationToken cancellationToken)
    {
        request.Headers.TryGetValue("If-Match", out var ifMatchHeader);
        request.Headers.TryGetValue("If-None-Match", out var ifNoneMatchHeader);

        var result = from id in TryGetOrderId(orderId)
                     from requestModel in TryGetRequestModel(body)
                     from putAction in TryGetPutAction(ifMatchHeader, ifNoneMatchHeader)
                     select ProcessPutAction(putAction, id, requestModel, request.GetUri(), createOrder, findOrder, updateOrder, cancellationToken);

        return await result.Coalesce();
    }

    private static Either<IResult, OrderId> TryGetOrderId(string orderId)
    {
        return Common.TryGetOrderId(orderId)
                     .MapLeft(GetIdErrorToResult);
    }

    private static IResult GetIdErrorToResult(string error)
    {
        return TypedResults.BadRequest(new
        {
            code = nameof(ErrorCode.InvalidId),
            message = error
        });
    }

    private static Either<IResult, RequestModel> TryGetRequestModel(JsonNode? body)
    {
        return TryDeserializeRequestModel(body)
                .ToEither()
                .MapLeft(errors => TypedResults.BadRequest(new
                {
                    code = nameof(ErrorCode.InvalidJsonBody),
                    message = "Request body is invalid.",
                    details = errors.Map(error => new
                    {
                        code = nameof(ErrorCode.InvalidJsonBody),
                        message = error
                    })
                }) as IResult);
    }

    private static Validation<string, RequestModel> TryDeserializeRequestModel(JsonNode? body)
    {
        if (body is not JsonObject jsonObject)
        {
            return "Request body must be a JSON object.";
        }

        return jsonObject.TryGetJsonObjectArrayProperty("pizzas")
                         .ToValidation()
                         .Bind(pizzaJsons => pizzaJsons.Map(Serialization.TryDeserializePizza)
                                                       .Sequence())
                         .Map(pizzas => new RequestModel
                         {
                             Pizzas = pizzas.Freeze()
                         });
    }

    private static Either<IResult, PutAction> TryGetPutAction(StringValues ifMatchHeader, StringValues ifNoneMatchHeader)
    {
        return (ifMatchHeader.ToArray(), ifNoneMatchHeader.ToArray()) switch
        {
            ([], []) => TypedResults.Json(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "Must specify 'If-Match' or 'If-None-Match' header."
            }, statusCode: StatusCodes.Status428PreconditionRequired),
            ({ Length: > 0 }, { Length: > 0 }) => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "Cannot specify both 'If-Match' and 'If-None-Match' headers."
            }),
            ([], [var ifNoneMatch]) when string.IsNullOrWhiteSpace(ifNoneMatch) => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "'If-None-Match' header must be '*'."
            }),
            ([], [var ifNoneMatch]) when new ETag(ifNoneMatch!).Value == ETag.All.Value => new PutAction.Create(),
            ([], _) => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "'If-None-Match' header must be '*'."
            }),
            ([var ifMatch], []) when string.IsNullOrWhiteSpace(ifMatch) => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "'If-Match' header cannot be empty."
            }),
            ([var ifMatch], []) => new PutAction.Update(new(ifMatch!)),
            (_, []) => TypedResults.BadRequest(new
            {
                code = nameof(ErrorCode.InvalidConditionalHeader),
                message = "'Must specify exactly one 'If-Match' header."
            })
        };
    }

    private record PutAction
    {
        public sealed record Create : PutAction;
        public sealed record Update(ETag IfMatchETag) : PutAction;
    }

    private static async ValueTask<IResult> ProcessPutAction(PutAction putAction,
                                                             OrderId orderId,
                                                             RequestModel requestModel,
                                                             Uri requestUri,
                                                             CreateOrder createOrder,
                                                             FindOrder findOrder,
                                                             UpdateOrder updateOrder,
                                                             CancellationToken cancellationToken)
    {
        return await (putAction switch
        {
            PutAction.Create => TryCreateOrder(orderId, requestModel, requestUri, createOrder, cancellationToken),
            PutAction.Update updateAction => TryUpdateOrder(orderId, requestModel, updateAction.IfMatchETag, findOrder, updateOrder, cancellationToken),
            _ => throw new NotImplementedException()
        });
    }

    private static async ValueTask<IResult> TryCreateOrder(OrderId orderId,
                                                           RequestModel requestModel,
                                                           Uri requestUri,
                                                           CreateOrder createOrder,
                                                           CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = orderId,
            Pizzas = requestModel.Pizzas,
            Status = new OrderStatus.New()
        };

        var result = await createOrder(order, cancellationToken);

        return result.Match(eTag => GetSuccessfulCreateResponse(order, eTag, requestUri),
                            GetCreateErrorResponse);
    }

    private static IResult GetSuccessfulCreateResponse(Order order, ETag eTag, Uri requestUri)
    {
        var locationUri = requestUri.RemoveQuery().ToUri();

        var json = Serialization.Serialize(order);
        json.AddProperty("eTag", eTag.Value);
        json.Remove("id");

        return TypedResults.Created(locationUri, json);
    }

    private static IResult GetCreateErrorResponse(CreateError error)
    {
        return error switch
        {
            CreateError.ResourceAlreadyExists => TypedResults.Conflict(new
            {
                code = nameof(ErrorCode.ResourceAlreadyExists),
                message = "An order with the given ID already exists."
            }),
            _ => throw new NotImplementedException()
        };
    }

    private static async ValueTask<IResult> TryUpdateOrder(OrderId orderId,
                                                           RequestModel requestModel,
                                                           ETag eTag,
                                                           FindOrder findOrder,
                                                           UpdateOrder updateOrder,
                                                           CancellationToken cancellationToken)
    {
        var findOrderResult = await findOrder(orderId, cancellationToken);

        return await findOrderResult.MatchAsync(async order => await TryUpdateOrder(requestModel, order, eTag, updateOrder, cancellationToken),
                                                GetNotFoundResponse);
    }

    private static IResult GetNotFoundResponse()
    {
        return TypedResults.NotFound(new
        {
            code = nameof(ErrorCode.ResourceNotFound),
            message = "Order with ID was not found."
        });
    }

    private static async ValueTask<IResult> TryUpdateOrder(RequestModel requestModel,
                                                           Order order,
                                                           ETag eTag,
                                                           UpdateOrder updateOrder,
                                                           CancellationToken cancellationToken)
    {
        var updatedOrder = order with
        {
            Pizzas = requestModel.Pizzas
        };

        var updateResult = await updateOrder(order, eTag, cancellationToken);

        return updateResult.Match(newETag => GetSuccessfulUpdateResponse(updatedOrder, newETag),
                                  GetUpdateErrorResponse);
    }


    private static IResult GetSuccessfulUpdateResponse(Order order, ETag eTag)
    {
        var json = Serialization.Serialize(order);
        json.AddProperty("eTag", eTag.Value);
        json.Remove("id");

        return TypedResults.Ok(json);
    }

    private static IResult GetUpdateErrorResponse(UpdateError error)
    {
        return error switch
        {
            UpdateError.ResourceDoesNotExist => TypedResults.NotFound(new
            {
                code = nameof(ErrorCode.ResourceNotFound),
                message = "A resource with the given ID does not exist."
            }),
            UpdateError.ETagMismatch => TypedResults.Json(new
            {
                code = nameof(ErrorCode.ETagMismatch),
                message = "The eTag passed in the 'If-Match' header is invalid. Another process might have updated the resource.",
            }, statusCode: StatusCodes.Status412PreconditionFailed),
            _ => throw new NotImplementedException()
        };
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {

    }
}