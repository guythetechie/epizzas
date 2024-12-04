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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace api.v1.Orders;

internal delegate Eff<ImmutableArray<(Order, ETag)>> ListCosmosOrders();

internal static class ListModule
{
    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/", handle);

        static async ValueTask<IResult> handle([FromServices] ListCosmosOrders listCosmosOrders, CancellationToken cancellationToken)
        {
            var operation = from orders in ApiOperation.Lift(listCosmosOrders())
                            select getSuccessfulResponse(orders);

            return await operation.Run(cancellationToken);
        };

        static IResult getSuccessfulResponse(IEnumerable<(Order, ETag)> orders) =>
            Results.Ok(new JsonObject
            {
                ["value"] = orders.Select(order => Order.Serialize(order.Item1)
                                                        .SetProperty("eTag", order.Item2.ToString()))
                                  .ToJsonArray()
            });
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder)
    {
        ConfigureListCosmosOrders(builder);
    }

    private static void ConfigureListCosmosOrders(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetListCosmosOrders);
    }

    private static ListCosmosOrders GetListCosmosOrders(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return () =>
        {
            using var _ = activitySource.StartActivity("cosmos.list_orders");

            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition("SELECT c.orderId, c.status, c.pizzas, c._etag FROM c")
            };

            return from queryResults in common.CosmosModule.GetQueryResults(container, query)
                   from orders in queryResults.AsIterable()
                                              .Traverse(jsonObject => from eTag in common.CosmosModule.GetETag(jsonObject)
                                                                      from order in Order.Deserialize(jsonObject)
                                                                      select (order, eTag))
                                              .ToEff()
                   select orders.ToImmutableArray();
        };
    }
}