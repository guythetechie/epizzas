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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace api.v1.Orders;

internal delegate OptionT<Eff, (Order, ETag)> FindOrder(OrderId orderId);

internal static class GetByIdModule
{
    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{orderId}", handle);

        static async ValueTask<IResult> handle(string orderId, [FromServices] FindOrder findOrder, CancellationToken cancellationToken)
        {
            var operation = from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
                            from orderAndETag in ApiOperation.Lift(getOrder(validatedOrderId, findOrder))
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

        static EitherT<IResult, Eff, (Order, ETag)> getOrder(OrderId orderId, FindOrder findOrder) =>
            findOrder(orderId).ToEither(() => Results.NotFound(new
            {
                code = ApiErrorCode.ResourceNotFound.Instance.ToString(),
                message = $"Order with ID {orderId} was not found."
            }));

        static IResult getSuccessfulResponse(Order order, ETag eTag) =>
            Results.Ok(new
            {
                eTag = eTag.ToString(),
                pizzas = order.Pizzas
                              .Select(pizza => new
                              {
                                  size = pizza.Size.ToString(),
                                  toppings = pizza.Toppings
                                                  .Select(toppingAmount => new
                                                  {
                                                      topping = toppingAmount.Key.ToString(),
                                                      amount = toppingAmount.Value.ToString()
                                                  })
                                                  .ToImmutableArray()
                              })
                              .ToImmutableArray()
            });
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder)
    {
        ConfigureFindOrder(builder);
    }

    private static void ConfigureFindOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetFindOrder);
    }

    private static FindOrder GetFindOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return orderId =>
        {
            using var _ = activitySource.StartActivity("find_order")
                                       ?.AddTag("order_id", orderId);

            return from cosmosId in CosmosId.From(orderId.ToString())
                                            .ToEff()
                   let partitionKey = new PartitionKey(cosmosId.Value)
                   from record in common.CosmosModule
                                        .ReadRecord(container, cosmosId, partitionKey, CosmosModule.DeserializeOrder)
                   select (record.Record, record.ETag);
        };
    }
}