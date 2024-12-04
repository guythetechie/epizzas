using Azure;
using common;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace api.v1.Orders;

internal delegate Eff<Option<(Order, ETag)>> FindCosmosOrder(OrderId orderId);

internal static class GetByIdModule
{
    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{orderId}", handle);

        static async ValueTask<IResult> handle(string orderId, [FromServices] FindCosmosOrder findCosmosOrder, CancellationToken cancellationToken)
        {
            var operation = from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
                            from orderAndETag in ApiOperation.Lift(getOrder(validatedOrderId, findCosmosOrder))
                            let successfulResponse = getSuccessfulResponse(orderAndETag.Item1, orderAndETag.Item2)
                            select successfulResponse;

            return await operation.Run(cancellationToken);
        };

        static Either<IResult, OrderId> validateOrderId(string orderId) =>
            OrderId.From(orderId)
                   .ToEither()
                   .MapLeft(error => Results.BadRequest(new
                   {
                       code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                       message = error.Message
                   }));

        static Eff<Either<IResult, (Order, ETag)>> getOrder(OrderId orderId, FindCosmosOrder findCosmosOrder) =>
            from option in findCosmosOrder(orderId)
            select option.ToEither(() => Results.NotFound(new
            {
                code = ApiErrorCode.ResourceNotFound.Instance.ToString(),
                message = $"Order with ID {orderId} was not found."
            }));

        static IResult getSuccessfulResponse(Order order, ETag eTag) =>
            Results.Ok(new JsonObject
            {
                ["eTag"] = eTag.ToString(),
                ["status"] = OrderStatus.Serialize(order.Status),
                ["pizzas"] = order.Pizzas
                                  .Select(Pizza.Serialize)
                                  .ToJsonArray()
            });
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder)
    {
        ConfigureFindCosmosOrder(builder);
    }

    private static void ConfigureFindCosmosOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetFindCosmosOrder);
    }

    private static FindCosmosOrder GetFindCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return orderId =>
        {
            using var _ = activitySource.StartActivity("cosmos.find_order")
                                       ?.AddTag("order_id", orderId);

            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition("""
                                            SELECT c.orderId, c.status, c.pizzas, c._etag
                                            FROM c
                                            WHERE c.orderId = @orderId
                                            """
                                            ).WithParameter("@orderId", orderId.ToString())
            };

            return from queryResults in common.CosmosModule.GetQueryResults(container, query)
                   let option = from json in queryResults.HeadOrNone()
                                let eTag = common.CosmosModule.GetETag(json).ThrowIfFail()
                                let order = Order.Deserialize(json).ThrowIfFail()
                                select (order, eTag)
                   select option;
        };
    }
}