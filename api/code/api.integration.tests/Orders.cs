using api.integration.tests;
using common;
using CsCheck;
using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace api.integration.tests;

internal delegate ValueTask RunOrderTests(CancellationToken cancellationToken);

internal static class OrdersModule
{
    public static void ConfigureRunOrderTests(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureEmptyOrdersContainer(builder);
        ApiModule.ConfigureListOrders(builder);
        ApiModule.ConfigureCreateOrder(builder);
        ApiModule.ConfigureCancelOrder(builder);
        ApiModule.ConfigureFindOrder(builder);

        builder.Services.TryAddSingleton(GetRunOrderTests);
    }

    private static RunOrderTests GetRunOrderTests(IServiceProvider provider)
    {
        var activitySource = provider.GetRequiredService<ActivitySource>();
        var emptyOrdersContainer = provider.GetRequiredService<EmptyOrdersContainer>();
        var listOrders = provider.GetRequiredService<ListOrders>();
        var createOrder = provider.GetRequiredService<CreateOrder>();
        var cancelOrder = provider.GetRequiredService<CancelOrder>();
        var findOrder = provider.GetRequiredService<FindOrder>();

        return async cancellationToken =>
        {
            using var activity = activitySource.StartActivity(nameof(RunOrderTests));

            var generator = from orders in Generator.Order.FrozenSetOf((first, second) => first?.Id == second?.Id,
                                                                       order => order.Id.GetHashCode())
                            from ordersToCancel in Generator.SubFrozenSetOf(orders)
                            from missingOrderId in Generator.OrderId
                            where orders.All(order => order.Id != missingOrderId)
                            select (orders, ordersToCancel, missingOrderId);

            await generator.SampleAsync(async x =>
            {
                var (orders, ordersToCancel, missingOrderId) = x;

                await ensure_no_orders_after_emptying_container(cancellationToken);
                await ensure_orders_are_created(orders, cancellationToken);
                await ensure_orders_are_canceled(ordersToCancel, cancellationToken);
            }, iter: 1);

            await emptyOrdersContainer(cancellationToken);
        };

        async ValueTask ensure_no_orders_after_emptying_container(CancellationToken cancellationToken)
        {
            using var _ = activitySource.StartActivity(nameof(ensure_no_orders_after_emptying_container));

            await emptyOrdersContainer(cancellationToken);
            using var response = await listOrders(cancellationToken);
            var orders = await listOrdersFromResponse(response, cancellationToken);
            orders.Should().BeEmpty();
        }

        async ValueTask<ImmutableArray<(Order, ETag)>> listOrdersFromResponse(HttpResponseMessage response,
                                                                              CancellationToken cancellationToken)
        {
            var result = from jsonNode in await JsonNodeModule.From(await response.Content.ReadAsStreamAsync(cancellationToken),
                                                                    cancellationToken: cancellationToken)
                         from jsonObject in jsonNode.AsJsonObject()
                         from jsonArray in jsonObject.GetJsonArrayProperty("values")
                         from orders in jsonArray.AsIterable().Traverse(getOrderAndETag).As()
                         select orders;

            return [.. result.ThrowIfFail()];
        }

        static JsonResult<(Order, ETag)> getOrderAndETag(JsonNode? node) =>
            from order in Order.Deserialize(node)
            from jsonObject in node.AsJsonObject()
            from eTagString in jsonObject.GetStringProperty("eTag")
            let eTag = ETag.From(eTagString)
            select (order, eTag.ThrowIfFail());

        async ValueTask ensure_orders_are_created(IEnumerable<Order> orders, CancellationToken cancellationToken)
        {
            using var _ = activitySource.StartActivity(nameof(ensure_orders_are_created));

            await orders.IterParallel(async order =>
            {
                // Create order
                using var createResponse = await createOrder(order, cancellationToken);
                createResponse.Should().BeSuccessful();

                // Find order
                using var findResponse = await findOrder(order.Id, cancellationToken);
                findResponse.Should().BeSuccessful();

                // Creating again should fail
                using var secondCreateResponse = await createOrder(order, cancellationToken);
                secondCreateResponse.Should().BeUnsuccessful().And.HaveStatusCode(HttpStatusCode.Conflict);
            }, cancellationToken);
        }

        async ValueTask ensure_orders_are_canceled(IEnumerable<Order> orders, CancellationToken cancellationToken)
        {
            using var _ = activitySource.StartActivity(nameof(ensure_orders_are_canceled));

            await orders.IterParallel(async order =>
            {
                using var firstFindResponse = await findOrder(order.Id, cancellationToken);
                var (_, eTag) = await getOrderFromResponse(order.Id, firstFindResponse, cancellationToken);

                using var cancelResponse = await cancelOrder(order.Id, eTag, cancellationToken);
                cancelResponse.Should().BeSuccessful();

                using var secondFindResponse = await findOrder(order.Id, cancellationToken);
                var (cancelledOrder, _) = await getOrderFromResponse(order.Id, secondFindResponse, cancellationToken);
                cancelledOrder.Status.Should().BeOfType<OrderStatus.Cancelled>();
            }, cancellationToken);
        }

        async ValueTask<(Order, ETag)> getOrderFromResponse(OrderId orderId,
                                                            HttpResponseMessage response,
                                                            CancellationToken cancellationToken)
        {
            var result = from jsonNode in await JsonNodeModule.From(await response.Content.ReadAsStreamAsync(cancellationToken),
                                                                    cancellationToken: cancellationToken)
                         from jsonObject in jsonNode.AsJsonObject()
                         let jsonObjectWithOrderId = jsonObject.SetProperty("orderId", OrderId.Serialize(orderId))
                         from orderAndETag in getOrderAndETag(jsonObjectWithOrderId)
                         select orderAndETag;

            return result.ThrowIfFail();
        }
    }
}