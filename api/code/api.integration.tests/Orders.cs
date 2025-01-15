using api.integration.tests;
using common;
using CsCheck;
using FluentAssertions;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
                using var createResponse = await createOrder(order, cancellationToken);
                createResponse.Should().BeSuccessful();

                using var findResponse = await findOrder(order.Id, cancellationToken);
                findResponse.Should().BeSuccessful();
            }, cancellationToken);
        }

        async ValueTask ensure_orders_are_canceled(IEnumerable<Order> orders, CancellationToken cancellationToken)
        {
            using var _ = activitySource.StartActivity(nameof(ensure_orders_are_canceled));

            await orders.IterParallel(async order =>
            {
                using var createResponse = await createOrder(order, cancellationToken);
                createResponse.Should().BeSuccessful();

                using var firstFindResponse = await findOrder(order.Id, cancellationToken);
                var (_, eTag) = await getOrderFromResponse(firstFindResponse, cancellationToken);

                using var _ = await cancelOrder(order.Id, eTag, cancellationToken);
                using var secondFindResponse = await findOrder(order.Id, cancellationToken);
                var (cancelledOrder, _) = await getOrderFromResponse(secondFindResponse, cancellationToken);
                cancelledOrder.Status.Should().BeOfType<OrderStatus.Cancelled>();
            }, cancellationToken);
        }

        async ValueTask<(Order, ETag)> getOrderFromResponse(HttpResponseMessage response,
                                                            CancellationToken cancellationToken)
        {
            var result = from jsonNode in await JsonNodeModule.From(await response.Content.ReadAsStreamAsync(cancellationToken),
                                                                    cancellationToken: cancellationToken)
                         from orderAndETag in getOrderAndETag(jsonNode)
                         select orderAndETag;

            return result.ThrowIfFail();
        }
    }
}

//internal delegate ValueTask TestOrders(CancellationToken cancellationToken);

//internal static class OrdersModule
//{
//    public static void ConfigureBuilder(IHostApplicationBuilder builder)
//    {
//        HttpModule.ConfigureBuilder(builder);
//        CosmosModule.ConfigureOrdersContainer(builder);

//        builder.Services.TryAddSingleton(GetTestOrders);
//    }

//    private static TestOrders GetTestOrders(IServiceProvider provider)
//    {
//        var ordersContainer = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
//        var clientFactory = provider.GetRequiredService<IHttpClientFactory>();

//        return async cancellationToken =>
//        {
//            await setUpDatabase(cancellationToken);
//            await generator.SampleAsync(
//                async order =>
//                {
//                    // Ensure order does not exist
//                    var orderOption1 = await findOrder(order.Id).RunUnsafe(cancellationToken);
//                    orderOption1.Should().BeNone();

//                    // Invalid order fails
//                    var invalidOrderJson = invalidateOrder(Order.Serialize(order)).Single();
//                    using var response1 = await createOrder(invalidOrderJson, order.Id).RunUnsafe(cancellationToken);
//                    response1.Should().HaveStatusCode(HttpStatusCode.BadRequest);

//                    // Valid order is created successfully
//                    var orderJson = Order.Serialize(order);
//                    using var response2 = await createOrder(orderJson, order.Id).RunUnsafe(cancellationToken);
//                    response2.Should().BeSuccessful();

//                    // List orders contains order
//                    var orders = await listOrders().RunUnsafe(cancellationToken);
//                    orders.Should().Contain(x => x.Order.Id == order.Id);

//                    // Cancel order without eTag fails
//                    using var response3 = await cancelOrder(order.Id, None).RunUnsafe(cancellationToken);
//                    response3.Should().HaveStatusCode(HttpStatusCode.BadRequest);

//                    // Cancel order with invalid eTag fails
//                    var invalidETag = new ETag("\"invalid\"");
//                    using var response4 = await cancelOrder(order.Id, Some(invalidETag)).RunUnsafe(cancellationToken);
//                    response4.Should().HaveStatusCode(HttpStatusCode.PreconditionFailed);

//                    // Cancel order with valid eTag succeeds
//                    var (_, eTag3) = await getOrder(order.Id).RunUnsafe(cancellationToken);
//                    using var response5 = await cancelOrder(order.Id, Some(eTag3)).RunUnsafe(cancellationToken);
//                    response5.Should().BeSuccessful();
//                    var (order3, _) = await getOrder(order.Id).RunUnsafe(cancellationToken);
//                    order3.Status.Should().BeOfType<OrderStatus.Cancelled>();
//                },
//                iter: 100
//            );
//        };

//        Eff<Option<(Order Order, ETag ETag)>> findOrder(OrderId orderId) =>
//            from cancellationToken in cancelTokenEff
//            let uri = new Uri($"/orders/{orderId}", UriKind.Relative)
//            from client in use(() => clientFactory.CreateClient(HttpModule.ApiClientKey))
//            from response in use(liftEff(async () => await client.GetAsync(uri, cancellationToken)))
//            from option in response.StatusCode switch
//            {
//                HttpStatusCode.NotFound => Pure(Option<(Order, ETag)>.None),
//                _ => from _ in liftEff(response.EnsureSuccessStatusCode)
//                from jsonObject in getJsonObjectFromResponse(response)
//                let jsonObjectWithId = jsonObject.SetProperty("orderId", OrderId.Serialize(orderId))
//                from order in (
//                    from order in Order.Deserialize(jsonObjectWithId)
//                    from eTagString in jsonObject.GetStringProperty("eTag")
//                    let eTag = new ETag(eTagString)
//                    select (order, eTag)
//                ).ToEff()
//                select Some(order),
//            }
//            select option;

//        Eff<(Order Order, ETag ETag)> getOrder(OrderId orderId) =>
//            from option in findOrder(orderId)
//            select option.IfNone(() => throw new InvalidOperationException("Order should exist."));

//        Eff<ImmutableArray<(Order Order, ETag ETag)>> listOrders() =>
//            from cancellationToken in cancelTokenEff
//            let uri = new Uri("/orders", UriKind.Relative)
//            from client in use(() => clientFactory.CreateClient(HttpModule.ApiClientKey))
//            from response in use(liftEff(async () => await client.GetAsync(uri, cancellationToken)))
//            let _ = response.EnsureSuccessStatusCode()
//            from jsonObject in getJsonObjectFromResponse(response)
//            from orders in (
//                from value in jsonObject.GetJsonArrayProperty("value")
//                from orders in value
//                    .AsIterable()
//                    .Traverse(node =>
//                        from resultJson in node.AsJsonObject()
//                        from order in Order.Deserialize(resultJson)
//                        from eTagString in resultJson.GetStringProperty("eTag")
//                        let eTag = new ETag(eTagString)
//                        select (order, eTag)
//                    )
//                select orders.ToImmutableArray()
//            ).ToEff()
//            select orders;

//        static Eff<JsonObject> getJsonObjectFromResponse(HttpResponseMessage response) =>
//            from cancellationToken in cancelTokenEff
//            from responseStream in use(liftEff(async () => await response.Content.ReadAsStreamAsync(cancellationToken)))
//            from binaryData in liftEff(async () => await BinaryData.FromStreamAsync(responseStream, cancellationToken))
//            from jsonObject in JsonNodeModule.Deserialize<JsonObject>(binaryData).ToEff()
//            select jsonObject;

//        Eff<HttpResponseMessage> createOrder(JsonNode? order, OrderId orderId) =>
//            from cancellationToken in cancelTokenEff
//            let uri = new Uri($"/orders/{orderId}", UriKind.Relative)
//            from client in use(() => clientFactory.CreateClient(HttpModule.ApiClientKey))
//            from content in use(() => JsonContent.Create(order))
//            from response in liftEff(async () => await client.PutAsync(uri, content))
//            select response;

//        Eff<HttpResponseMessage> cancelOrder(OrderId orderId, Option<ETag> eTag) =>
//            from cancellationToken in cancelTokenEff
//            let uri = new Uri($"/orders/{orderId}", UriKind.Relative)
//            from client in use(() => clientFactory.CreateClient(HttpModule.ApiClientKey))
//            from request in use(() => new HttpRequestMessage(HttpMethod.Delete, uri))
//            let _ = eTag.Iter(eTag => request.Headers.IfMatch.Add(new(eTag.ToString())))
//            from response in liftEff(async () => await client.SendAsync(request, cancellationToken))
//            select response;

//        static Gen<JsonNode?> invalidateOrder(JsonNode? order) =>
//            from invalidOrder in order switch
//            {
//                JsonObject jsonObject => Gen.OneOf<JsonNode?>(
//                    Gen.Const(jsonObject.RemoveProperty("pizzas")),
//                    from invalidPizzas in jsonObject
//                        .GetProperty("pizzas")
//                        .Match(invalidatePizzas, _ => invalidatePizzas(null))
//                    select jsonObject.SetProperty("pizzas", invalidPizzas)
//                ),
//                _ => Gen.Const(order),
//            }
//            select invalidOrder;

//        static Gen<JsonNode?> invalidatePizzas(JsonNode? pizzas) =>
//            from invalidPizzas in pizzas switch
//            {
//                JsonArray pizzasArray => pizzasArray switch
//                {
//                    [] => Gen.Const<JsonNode?>(pizzasArray),
//                    { Count: var count } => from index in Gen.Int[1, count]
//                    let pizza = pizzasArray[index]
//                    from invalidPizza in invalidatePizza(pizza)
//                    select (pizzasArray[index] = invalidPizza),
//                },
//                _ => Gen.Const(pizzas),
//            }
//            select invalidPizzas;

//        static Gen<JsonNode?> invalidatePizza(JsonNode? pizza) =>
//            from invalidPizza in pizza switch
//            {
//                JsonObject jsonObject => Gen.OneOf(
//                    JsonNodeGenerator.NonJsonObject.Null(),
//                    from p in Gen.Const(jsonObject)
//                    select p.RemoveProperty("size"),
//                    from invalidSize in jsonObject
//                        .GetProperty("size")
//                        .Match(invalidatePizzaSize, _ => invalidatePizzaSize(null))
//                    select jsonObject.SetProperty("size", invalidSize),
//                    from p in Gen.Const(jsonObject)
//                    select p.RemoveProperty("toppings"),
//                    from invalidToppings in jsonObject
//                        .GetProperty("toppings")
//                        .Match(invalidatePizzaToppings, _ => invalidatePizzaToppings(null))
//                    select jsonObject.SetProperty("toppings", invalidToppings)
//                ),
//                _ => Gen.Const(pizza),
//            }
//            select invalidPizza;

//        static Gen<JsonNode?> invalidatePizzaSize(JsonNode? size) =>
//            from invalidSize in Gen.OneOf(
//                JsonNodeGenerator.NonJsonValue.Null(),
//                JsonValueGenerator.NonString.Null(),
//                from value in JsonValueGenerator.String
//                where
//                    value.ToString()
//                        is not (nameof(PizzaSize.Small) or nameof(PizzaSize.Medium) or nameof(PizzaSize.Large))
//                select value
//            )
//            select invalidSize;

//        static Gen<JsonNode?> invalidatePizzaToppings(JsonNode? toppings) =>
//            from invalidToppings in toppings switch
//            {
//                JsonArray toppingsArray => toppingsArray switch
//                {
//                    [] => Gen.Const<JsonNode?>(toppingsArray),
//                    { Count: var count } => from index in Gen.Int[1, count]
//                    let topping = toppingsArray[index]
//                    from invalidTopping in invalidatePizzaTopping(topping)
//                    select (toppingsArray[index] = invalidTopping),
//                },
//                _ => Gen.Const(toppings),
//            }
//            select invalidToppings;

//        static Gen<JsonNode?> invalidatePizzaTopping(JsonNode? topping) =>
//            from invalidTopping in topping switch
//            {
//                JsonObject toppingObject => from invalidTopping in Gen.OneOf(
//                    JsonNodeGenerator.NonJsonObject.Null(),
//                    from t in Gen.Const(toppingObject)
//                    select t.RemoveProperty("kind"),
//                    from invalidKind in toppingObject
//                        .GetProperty("kind")
//                        .Match(invalidatePizzaToppingKind, _ => invalidatePizzaToppingKind(null))
//                    select toppingObject.SetProperty("kind", invalidKind),
//                    from t in Gen.Const(toppingObject)
//                    select t.RemoveProperty("amount"),
//                    from invalidAmount in toppingObject
//                        .GetProperty("amount")
//                        .Match(invalidatePizzaToppingAmount, _ => invalidatePizzaToppingAmount(null))
//                    select toppingObject.SetProperty("amount", invalidAmount)
//                )
//                select invalidTopping,
//                _ => Gen.Const(topping),
//            }
//            select invalidTopping;

//        static Gen<JsonNode?> invalidatePizzaToppingKind(JsonNode? kind) =>
//            from invalidKind in Gen.OneOf(
//                JsonNodeGenerator.NonJsonValue.Null(),
//                JsonValueGenerator.NonString.Null(),
//                from value in JsonValueGenerator.String
//                where
//                    value.ToString()
//                        is not (
//                            nameof(PizzaToppingKind.Cheese)
//                            or nameof(PizzaToppingKind.Pepperoni)
//                            or nameof(PizzaToppingKind.Sausage)
//                        )
//                select value
//            )
//            select invalidKind;

//        static Gen<JsonNode?> invalidatePizzaToppingAmount(JsonNode? amount) =>
//            from invalidAmount in Gen.OneOf(
//                JsonNodeGenerator.NonJsonValue.Null(),
//                JsonValueGenerator.NonString.Null(),
//                from value in JsonValueGenerator.String
//                where
//                    value.ToString()
//                        is not (
//                            nameof(PizzaToppingAmount.Light)
//                            or nameof(PizzaToppingAmount.Normal)
//                            or nameof(PizzaToppingAmount.Extra)
//                        )
//                select value
//            )
//            select invalidAmount;
//    }

//    private static void ConfigureSetUpDatabase(IHostApplicationBuilder builder)
//    {
//        builder.AddAzureCosmosClient(builder.Configuration.GetValueOrThrow("ASPIRE_COSMOS_CONNECTION_NAME"));

//        builder.Services.TryAddSingleton(GetSetUpDatabase);
//    }

//    private static SetUpDatabase GetSetUpDatabase(IServiceProvider provider)
//    {
//        var client = provider.GetRequiredService<CosmosClient>();
//        var configuration = provider.GetRequiredService<IConfiguration>();
//        var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger>();

//        var databaseName = configuration.GetValueOrThrow("COSMOS_DATABASE_NAME");
//        var containerName = configuration.GetValueOrThrow("COSMOS_ORDERS_CONTAINER_NAME");

//        return async cancellationToken =>
//        {
//            logger.LogInformation("Ensuring database exists...");
//            Database database = await client.CreateDatabaseIfNotExistsAsync(
//                databaseName,
//                cancellationToken: cancellationToken
//            );

//            logger.LogInformation("Ensuring container exists...");
//            Container container = await database.CreateContainerIfNotExistsAsync(
//                containerName,
//                "/orderId",
//                cancellationToken: cancellationToken
//            );

//            logger.LogInformation("Ensuring container is empty...");
//            var queryOptions = new CosmosQueryOptions
//            {
//                Query = new QueryDefinition("SELECT c.id, c._etag, c.orderId FROM c"),
//            };

//            var action =
//                from results in CosmosModule.GetQueryResults(container, queryOptions)
//                from _ in results
//                    .AsIterable()
//                    .Traverse(result =>
//                        from id in CosmosModule.GetCosmosId(result)
//                        from eTag in CosmosModule.GetETag(result)
//                        from orderId in result.GetStringProperty("orderId")
//                        let partitionKey = new PartitionKey(orderId)
//                        select (id, eTag, partitionKey)
//                    )
//                    .Traverse(results =>
//                        results.Traverse(x => CosmosModule.DeleteRecord(container, x.id, x.partitionKey, x.eTag))
//                    )
//                    .As()
//                select Unit.Default;

//            await action.RunUnsafeAsync(EnvIO.New(token: cancellationToken));
//        };
//    }
//}
