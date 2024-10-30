using common;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;

namespace api.v1.Orders;

internal static class CosmosModule
{
    public static string OrdersContainerIdentifier { get; } = nameof(OrdersContainerIdentifier);

    public static void ConfigureOrdersContainer(IHostApplicationBuilder builder)
    {
        ConfigureDatabase(builder);

        builder.Services.TryAddKeyedSingleton(OrdersContainerIdentifier,
                                              (provider, _) => GetOrdersContainer(provider));
    }

    private static Container GetOrdersContainer(IServiceProvider provider)
    {
        var database = provider.GetRequiredService<Database>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var containerName = configuration.GetValueOrThrow("COSMOS_ORDERS_CONTAINER_NAME");

        return database.GetContainer(containerName);
    }

    private static void ConfigureDatabase(IHostApplicationBuilder builder)
    {
        builder.AddAzureCosmosClient(string.Empty);

        builder.Services.TryAddSingleton(GetDatabase);
    }

    private static Database GetDatabase(IServiceProvider provider)
    {
        var client = provider.GetRequiredService<CosmosClient>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var databaseName = configuration.GetValueOrThrow("COSMOS_DATABASE_NAME");

        return client.GetDatabase(databaseName);
    }

    public static JsonObject SerializeOrder(Order order) =>
        new()
        {
            ["id"] = order.Id.ToString(),
            ["pizzas"] = order.Pizzas
                              .Select(pizza => new JsonObject
                              {
                                  ["size"] = pizza.Size.ToString(),
                                  ["toppings"] = pizza.Toppings
                                                      .Select(topping => new JsonObject
                                                      {
                                                          ["kind"] = topping.Key.ToString(),
                                                          ["amount"] = topping.Value.ToString()
                                                      })
                                                      .ToJsonArray()
                              })
                              .ToJsonArray()
        };

    public static Fin<Order> DeserializeOrder(JsonObject json) =>
        from id in DeserializeOrderId(json)
        from pizzas in DeserializePizzas(json)
        select new Order
        {
            Id = id,
            Pizzas = pizzas
        };

    private static Fin<OrderId> DeserializeOrderId(JsonObject orderJson) =>
        from idString in orderJson.GetStringProperty("id")
        from orderId in OrderId.From(idString)
        select orderId;

    private static Fin<ImmutableArray<Pizza>> DeserializePizzas(JsonObject orderJson) =>
        from pizzasArray in orderJson.GetJsonArrayProperty("pizzas")
        from pizzaJsonObjects in pizzasArray.AsIterable()
                                            .Traverse(node => node?.AsJsonObject())
        from pizzas in pizzaJsonObjects.Traverse(DeserializePizza)
        select pizzas.ToImmutableArray();

    private static Fin<Pizza> DeserializePizza(JsonObject pizzaJson) =>
        from size in DeserializePizzaSize(pizzaJson)
        from toppings in DeserializePizzaToppings(pizzaJson)
        select new Pizza
        {
            Size = size,
            Toppings = toppings
        };

    private static Fin<PizzaSize> DeserializePizzaSize(JsonObject pizzaJson) =>
        from sizeString in pizzaJson.GetStringProperty("size")
        from size in sizeString switch
        {
            _ when nameof(PizzaSize.Small).Equals(sizeString, StringComparison.OrdinalIgnoreCase) => Fin<PizzaSize>.Succ(PizzaSize.Small.Instance),
            _ when nameof(PizzaSize.Medium).Equals(sizeString, StringComparison.OrdinalIgnoreCase) => PizzaSize.Medium.Instance,
            _ when nameof(PizzaSize.Large).Equals(sizeString, StringComparison.OrdinalIgnoreCase) => PizzaSize.Large.Instance,
            _ => Error.New($"'{sizeString}' is not a valid pizza size.")
        }
        select size;

    private static Fin<FrozenDictionary<PizzaToppingKind, PizzaToppingAmount>> DeserializePizzaToppings(JsonObject pizzaJson) =>
        from toppingsArray in pizzaJson.GetJsonArrayProperty("toppings")
        from toppingJsonObjects in toppingsArray.AsIterable()
                                                .Traverse(node => node?.AsJsonObject())
        from toppings in toppingJsonObjects.Traverse(DeserializePizzaTopping)
        select toppings.ToFrozenDictionary();

    private static Fin<(PizzaToppingKind, PizzaToppingAmount)> DeserializePizzaTopping(JsonObject toppingJson) =>
        from amount in DeserializePizzaToppingAmount(toppingJson)
        from kind in DeserializePizzaToppingKind(toppingJson)
        select (kind, amount);

    private static Fin<PizzaToppingKind> DeserializePizzaToppingKind(JsonObject toppingJson) =>
        from kindString in toppingJson.GetStringProperty("kind")
        from kind in kindString switch
        {
            _ when nameof(PizzaToppingKind.Pepperoni).Equals(kindString, StringComparison.OrdinalIgnoreCase) => Fin<PizzaToppingKind>.Succ(PizzaToppingKind.Pepperoni.Instance),
            _ when nameof(PizzaToppingKind.Cheese).Equals(kindString, StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Cheese.Instance,
            _ when nameof(PizzaToppingKind.Sausage).Equals(kindString, StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Sausage.Instance,
            _ => Error.New($"'{kindString}' is not a valid pizza topping kind.")
        }
        select kind;

    private static Fin<PizzaToppingAmount> DeserializePizzaToppingAmount(JsonObject toppingJson) =>
        from amountString in toppingJson.GetStringProperty("amount")
        from amount in amountString switch
        {
            _ when nameof(PizzaToppingAmount.Light).Equals(amountString, StringComparison.OrdinalIgnoreCase) => Fin<PizzaToppingAmount>.Succ(PizzaToppingAmount.Light.Instance),
            _ when nameof(PizzaToppingAmount.Normal).Equals(amountString, StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Normal.Instance,
            _ when nameof(PizzaToppingAmount.Extra).Equals(amountString, StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Extra.Instance,
            _ => Error.New($"'{amountString}' is not a valid pizza topping amount.")
        }
        select amount;
}