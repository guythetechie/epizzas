using common;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace portal.Components.Orders;

#pragma warning disable CA1515 // Consider making public types internal
public delegate IAsyncEnumerable<(Order Order, ETag ETag)> ListOrders(CancellationToken cancellationToken);
public delegate ValueTask CreateOrder(Pizza pizza, CancellationToken cancellationToken);
public delegate OrderId GenerateOrderId();
#pragma warning restore CA1515 // Consider making public types internal

internal static class CommonModule
{
    public static void ConfigureListOrders(IHostApplicationBuilder builder)
    {
        ApiModule.ConfigureGetApiClient(builder);

        builder.Services.TryAddSingleton(GetListOrders);
    }

    private static ListOrders GetListOrders(IServiceProvider provider)
    {
        var getClient = provider.GetRequiredService<GetApiClient>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return listOrders;

        async IAsyncEnumerable<(Order Order, ETag ETag)> listOrders(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var activity = activitySource.StartActivity(nameof(ListOrders));

            var orderCount = 0;

            using var client = getClient();

            var uri = new Uri("/v1/orders", UriKind.Relative);
            using var response = await client.GetAsync(uri, cancellationToken);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var orders = from nodeResult in JsonNodeModule.FromStream(stream)
                         let ordersResult = from node in nodeResult
                                            from jsonObject in node.AsJsonObject()
                                            from jsonArray in jsonObject.GetJsonArrayProperty("values")
                                            from orders in jsonArray.AsIterable().Traverse(getOrderAndETag).As()
                                            select orders
                         select ordersResult.ThrowIfFail();

            foreach (var order in await orders.RunUnsafe(cancellationToken))
            {
                yield return order;
                orderCount++;
            }

            activity?.SetTag(nameof(orderCount), orderCount);
        }

        static JsonResult<(Order, ETag)> getOrderAndETag(JsonNode? node) =>
            from order in Order.Deserialize(node)
            from jsonObject in node.AsJsonObject()
            from eTagString in jsonObject.GetStringProperty("eTag")
            let eTag = ETag.From(eTagString)
            select (order, eTag.ThrowIfFail());
    }

    public static void ConfigureCreateOrder(IHostApplicationBuilder builder)
    {
        ApiModule.ConfigureGetApiClient(builder);
        ConfigureGenerateOrderId(builder);

        builder.Services.TryAddSingleton(GetCreateOrder);
    }

    private static CreateOrder GetCreateOrder(IServiceProvider provider)
    {
        var getClient = provider.GetRequiredService<GetApiClient>();
        var generateOrderId = provider.GetRequiredService<GenerateOrderId>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (pizza, cancellationToken) =>
        {
            using var activity = activitySource.StartActivity(nameof(CreateOrder))
                                              ?.AddTag(nameof(pizza), Pizza.Serialize(pizza));


            var orderId = generateOrderId();
            var pizzaJson = Pizza.Serialize(pizza);

            var orderJson = new JsonObject
            {
                ["orderId"] = OrderId.Serialize(orderId),
                ["pizzas"] = new JsonArray(pizzaJson)
            };

            using var client = getClient();
            var uri = new Uri("/v1/orders", UriKind.Relative);
            using var response = await client.PostAsJsonAsync(uri, orderJson, cancellationToken);
            response.EnsureSuccessStatusCode();
        };
    }

    public static void ConfigureGenerateOrderId(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(GetGenerateOrderId);
    }

    private static GenerateOrderId GetGenerateOrderId(IServiceProvider provider)
    {
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return () =>
        {
            using var activity = activitySource.StartActivity(nameof(GenerateOrderId));

            var orderId = OrderId.FromOrThrow(Guid.CreateVersion7().ToString());

            activity?.SetTag("orderId", orderId);

            return orderId;
        };
    }
}