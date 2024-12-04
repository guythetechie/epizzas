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
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace api.v1.Orders;

internal delegate Eff<Either<ApiErrorCode.ResourceAlreadyExists, Unit>> CreateCosmosOrder(Order order);

internal static class CreateModule
{
    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        builder.MapPut("/{orderId}", handle);

        static async ValueTask<IResult> handle([FromServices] CreateCosmosOrder createCosmosOrder, [FromServices] TimeProvider timeProvider, string orderId, Stream? body, CancellationToken cancellationToken)
        {
            var operation = from order in ApiOperation.Lift(parseOrder(orderId, body, timeProvider))
                            from _ in ApiOperation.Lift(createOrder(createCosmosOrder, order))
                            select getSuccessfulResponse();

            return await operation.Run(cancellationToken);
        }

        static Eff<Either<IResult, Order>> parseOrder(string orderId, Stream? body, TimeProvider timeProvider) =>
            from cancellationToken in Prelude.cancelTokenEff
            from data in Prelude.liftEff(async () => body is null ? null : await BinaryData.FromStreamAsync(body, cancellationToken))
            let orderResult = from jsonObject in JsonNodeModule.Deserialize<JsonObject>(data)
                              let status = new OrderStatus.Created
                              {
                                  Date = timeProvider.GetUtcNow(),
                                  By = "system"
                              }
                              let statusJson = OrderStatus.Serialize(status)
                              let orderJson = jsonObject.SetProperty("orderId", orderId)
                                                        .SetProperty("status", statusJson)
                              from order in Order.Deserialize(orderJson)
                              select order
            select orderResult.Match(Either<IResult, Order>.Right,
                                     error => Either<IResult, Order>.Left(Results.BadRequest(new
                                     {
                                         code = ApiErrorCode.InvalidRequestBody.Instance.ToString(),
                                         message = error.ToException().InnerException switch
                                         {
                                             AggregateException => "Request body is invalid.",
                                             _ => error.Message
                                         },
                                         details = error.ToException().InnerException switch
                                         {
                                             not AggregateException => Array.Empty<string>(),
                                             AggregateException aggregateException => [.. aggregateException.InnerExceptions.Select(exception => exception.Message)]
                                         }
                                     })));

        static Eff<Either<IResult, Unit>> createOrder(CreateCosmosOrder createCosmosOrder, Order order) =>
            from either in createCosmosOrder(order)
            select either.MapLeft(_ => Results.Conflict(new
            {
                code = ApiErrorCode.ResourceAlreadyExists.Instance.ToString(),
                message = $"Order with ID {order.Id} already exists."
            }));

        static IResult getSuccessfulResponse() =>
            Results.NoContent();
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        ConfigureCreateCosmosOrder(builder);
    }

    private static void ConfigureCreateCosmosOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetCreateCosmosOrder);
    }

    private static CreateCosmosOrder GetCreateCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return order =>
        {
            using var _ = activitySource.StartActivity("cosmos.create_order")
                                       ?.AddTag("order_id", order.Id);

            var cosmosOrderId = Guid.CreateVersion7();
            var orderJson = Order.Serialize(order)
                                 .AsJsonObject()
                                 .ThrowIfFail()
                                 .SetProperty("id", cosmosOrderId);
            var partitionKey = new PartitionKey(order.Id.ToString());

            return from either in common.CosmosModule.CreateRecord(container, orderJson, partitionKey)
                   select either.MapLeft(error => ApiErrorCode.ResourceAlreadyExists.Instance);
        };
    }
}