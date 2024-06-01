using common;
using LanguageExt;
using System;
using System.Text.Json.Nodes;

namespace api.v1.Orders;

internal static class Cosmos
{
    public static JsonObject SerializeOrder(Order order) =>
        new()
        {
            ["id"] = order.Id.ToString(),
            ["pizzas"] = order.Pizzas
                              .Map(pizza => new JsonObject
                              {
                                  ["size"] = pizza.Size.ToString(),
                                  ["toppings"] = pizza.Toppings
                                                      .AsEnumerable()
                                                      .Map(topping => new JsonObject
                                                      {
                                                          ["kind"] = topping.Key.ToString(),
                                                          ["amount"] = topping.Value.ToString()
                                                      })
                                                      .ToJsonArray()
                              })
                              .ToJsonArray()
        };

    public static Order DeserializeOrder(JsonObject json) =>
        new()
        {
            Id = DeserializeOrderId(json),
            Pizzas = DeserializePizzas(json)
        };

    private static OrderId DeserializeOrderId(JsonObject orderJson)
    {
        var idString = orderJson.GetStringProperty("id");
        return new OrderId(idString);
    }

    private static Seq<Pizza> DeserializePizzas(JsonObject orderJson) =>
        orderJson.GetJsonArrayProperty("pizzas")
                 .AsEnumerableM()
                 .Choose(node => node.TryAsJsonObject())
                 .Map(DeserializePizza)
                 .ToSeq();

    private static Pizza DeserializePizza(JsonObject pizzaJson) =>
        new()
        {
            Size = DeserializePizzaSize(pizzaJson),
            Toppings = DeserializePizzaToppings(pizzaJson)
        };

    private static PizzaSize DeserializePizzaSize(JsonObject pizzaJson)
    {
        var sizeString = pizzaJson.GetStringProperty("size");

        return sizeString switch
        {
            _ when nameof(PizzaSize.Small).Equals(sizeString, StringComparison.OrdinalIgnoreCase) => PizzaSize.Small.Instance,
            _ when nameof(PizzaSize.Medium).Equals(sizeString, StringComparison.OrdinalIgnoreCase) => PizzaSize.Medium.Instance,
            _ when nameof(PizzaSize.Large).Equals(sizeString, StringComparison.OrdinalIgnoreCase) => PizzaSize.Large.Instance,
            _ => throw new InvalidOperationException($"'{sizeString}' is not a valid pizza size.")
        };
    }

    private static HashMap<PizzaToppingKind, PizzaToppingAmount> DeserializePizzaToppings(JsonObject pizzaJson) =>
        pizzaJson.GetJsonArrayProperty("toppings")
                 .AsEnumerableM()
                 .Choose(node => node.TryAsJsonObject())
                 .Map(DeserializePizzaTopping)
                 .ToHashMap();

    private static (PizzaToppingKind, PizzaToppingAmount) DeserializePizzaTopping(JsonObject toppingJson) =>
        (DeserializePizzaToppingKind(toppingJson), DeserializePizzaToppingAmount(toppingJson));

    private static PizzaToppingKind DeserializePizzaToppingKind(JsonObject toppingJson)
    {
        var kindString = toppingJson.GetStringProperty("kind");

        return kindString switch
        {
            _ when nameof(PizzaToppingKind.Pepperoni).Equals(kindString, StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Pepperoni.Instance,
            _ when nameof(PizzaToppingKind.Cheese).Equals(kindString, StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Cheese.Instance,
            _ when nameof(PizzaToppingKind.Sausage).Equals(kindString, StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Sausage.Instance,
            _ => throw new InvalidOperationException($"'{kindString}' is not a valid pizza topping kind.")
        };
    }

    private static PizzaToppingAmount DeserializePizzaToppingAmount(JsonObject toppingJson)
    {
        var amountString = toppingJson.GetStringProperty("amount");

        return amountString switch
        {
            _ when nameof(PizzaToppingAmount.Light).Equals(amountString, StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Light.Instance,
            _ when nameof(PizzaToppingAmount.Normal).Equals(amountString, StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Normal.Instance,
            _ when nameof(PizzaToppingAmount.Extra).Equals(amountString, StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Extra.Instance,
            _ => throw new InvalidOperationException($"'{amountString}' is not a valid pizza topping amount.")
        };
    }
}