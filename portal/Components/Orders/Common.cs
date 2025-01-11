using common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace portal.Components.Orders;

#pragma warning disable CA1515 // Consider making public types internal
public delegate IAsyncEnumerable<(Order Order, ETag ETag)> ListOrders(CancellationToken cancellationToken);
#pragma warning restore CA1515 // Consider making public types internal

internal static class CommonModule
{
    public static void ConfigureListOrders(IHostApplicationBuilder builder)
    {
        ApiModule.ConfigureGetApiClientResponse(builder);

        builder.Services.TryAddSingleton(GetListOrders);
    }

    private static ListOrders GetListOrders(IServiceProvider provider)
    {
        var getApiClientResponse = provider.GetRequiredService<GetApiClientResponse>();

        return listOrders;

        async IAsyncEnumerable<(Order Order, ETag ETag)> listOrders(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var response = await getApiClientResponse(
                "/v1/orders",
                async (client, uri) => await client.GetAsync(uri, cancellationToken));

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var node = JsonResult.throwIfFail(await JsonNode.fromStream(stream).ToTask(cancellationToken));

            var ordersResult = from jsonObject in JsonNode.asJsonObject(node)
                               from jsonArray in JsonObject.getJsonArrayProperty("values", jsonObject)
                               from orders in jsonArray.Traverse(getOrderAndETag)
                               select orders;

            foreach (var order in ordersResult.ThrowIfFail())
            {
                yield return order;
            }
        }

        static JsonResult<(Order, ETag)> getOrderAndETag(System.Text.Json.Nodes.JsonNode? node) =>
            from order in common.Serialization.Order.deserialize(node)
            from jsonObject in JsonNode.asJsonObject(node)
            from eTagString in JsonObject.getStringProperty("eTag", jsonObject)
            let eTagResult = ETag.fromString(eTagString)
            from eTag in JsonResult.fromResult(eTagResult)
            select (order, eTag);
    }
}