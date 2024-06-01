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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace api.v1.Orders;

internal delegate IO<Seq<(Order, ETag)>> ListOrders();

internal static class ListEndpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/", Handle);
    }

    private static IResult Handle([FromServices] ListOrders listOrders, CancellationToken cancellationToken)
    {
        var operation = from orders in ApiOperation.LiftIO(listOrders())
                        from successfulResponse in ApiOperation.Pure(GetSuccessfulResponse(orders))
                        select successfulResponse;

        return operation.Run(cancellationToken);
    }

    private static IResult GetSuccessfulResponse(IEnumerable<(Order, ETag)> orders) =>
        Results.Ok(new
        {
            value = orders.Select(order => new
            {
                id = order.Item1.Id.ToString(),
                eTag = order.Item2.ToString(),
                pizzas = order.Item1
                              .Pizzas
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
            })
        });
}

internal static class ListServices
{
    public static void Configure(IServiceCollection services)
    {
        ConfigureListOrders(services);
    }

    private static void ConfigureListOrders(IServiceCollection services)
    {
        Services.ConfigureOrdersCosmosContainer(services);

        services.TryAddSingleton(ListOrders);
    }

    private static ListOrders ListOrders(IServiceProvider provider)
    {
        var container = provider.GetRequiredService<OrdersCosmosContainer>();

        return () =>
        {
            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition("SELECT c.id, c.pizzas, c._etag FROM c")
            };

            return from results in CosmosModule.GetQueryResults(container.Value, query)
                   select from json in results
                          let order = Cosmos.DeserializeOrder(json)
                          let eTag = CosmosModule.GetETag(json)
                          select (order, eTag);
        };
    }
}