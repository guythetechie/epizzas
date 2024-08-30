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
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace api.v1.Orders;

internal delegate OptionT<IO, (Order, ETag)> FindOrder(OrderId orderId);

internal static class GetByIdEndpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{orderId}", Handle);
    }

    private static IResult Handle(string orderId, [FromServices] FindOrder findOrder, CancellationToken cancellationToken)
    {
        var operation = from validatedOrderId in ApiOperation.LiftEither(ValidateOrderId(orderId))
                        from orderAndETag in ApiOperation.LiftEither(FindOrder(validatedOrderId, findOrder))
                        from successfulResponse in ApiOperation.Pure(GetSuccessfulResponse(orderAndETag.Item1, orderAndETag.Item2))
                        select successfulResponse;

        return operation.Run(cancellationToken);
    }

    private static Either<IResult, OrderId> ValidateOrderId(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            var result = Results.BadRequest(new
            {
                code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                message = "Order ID cannot be empty."
            });

            return Prelude.Left(result);
        }

        return new OrderId(orderId);
    }

    private static EitherT<IResult, IO, (Order, ETag)> FindOrder(OrderId orderId, FindOrder findOrder) =>
        findOrder(orderId)
            .ToEither(() => Results.NotFound(new
            {
                code = ApiErrorCode.ResourceNotFound.Instance.ToString(),
                message = $"Order with ID {orderId} was not found."
            }));

    private static IResult GetSuccessfulResponse(Order order, ETag eTag) =>
        Results.Ok(new
        {
            eTag = eTag.ToString(),
            pizzas = order.Pizzas
                          .Map(pizza => new
                          {
                              size = pizza.Size.ToString(),
                              toppings = pizza.Toppings
                                              .AsEnumerable()
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

internal static class GetByIdServices
{
    public static void Configure(IServiceCollection services)
    {
        Services.ConfigureOrdersCosmosContainer(services);

        services.TryAddSingleton(FindOrder);
    }

    private static FindOrder FindOrder(IServiceProvider provider)
    {
        var activitySource = provider.GetRequiredService<ActivitySource>();
        var container = provider.GetRequiredService<OrdersCosmosContainer>();

        return orderId =>
        {
            using var _ = activitySource.StartActivity(nameof(FindOrder));

            var cosmosId = new CosmosId(orderId.Value);
            var partitionKey = new PartitionKey(cosmosId.Value);

            return from cosmosRecord in CosmosModule.TryReadRecord(container.Value, cosmosId, partitionKey, Cosmos.DeserializeOrder)
                   select (cosmosRecord.Record, cosmosRecord.ETag);
        };
    }
}