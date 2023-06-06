using EPizzas.Common;
using LanguageExt;
using System;
using System.Text.Json.Nodes;

namespace EPizzas.Ordering.Api.V1.Orders;

#pragma warning disable CA1724 // Type names should not match namespaces
public static class Serialization
#pragma warning restore CA1724 // Type names should not match namespaces
{
    public static JsonValue Serialize(ToppingType value)
    {
        return value switch
        {
            ToppingType.Pepperoni => JsonValue.Create(nameof(ToppingType.Pepperoni)),
            ToppingType.Ham => JsonValue.Create(nameof(ToppingType.Ham)),
            ToppingType.Cheese => JsonValue.Create(nameof(ToppingType.Cheese)),
            _ => throw new NotImplementedException()
        };
    }

    public static Validation<string, ToppingType> TryDeserializeToppingType(JsonNode? jsonNode)
    {
        return jsonNode switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
            {
                nameof(ToppingType.Pepperoni) => new ToppingType.Pepperoni(),
                nameof(ToppingType.Ham) => new ToppingType.Ham(),
                nameof(ToppingType.Cheese) => new ToppingType.Cheese(),
                var _ => $"'{value}' is not a valid topping type."
            },
            _ => "Topping type must be a string value."
        };
    }

    public static JsonValue Serialize(ToppingAmount value)
    {
        return value switch
        {
            ToppingAmount.Light => JsonValue.Create(nameof(ToppingAmount.Light)),
            ToppingAmount.Medium => JsonValue.Create(nameof(ToppingAmount.Medium)),
            ToppingAmount.Extra => JsonValue.Create(nameof(ToppingAmount.Extra)),
            _ => throw new NotImplementedException()
        };
    }

    public static Validation<string, ToppingAmount> TryDeserializeToppingAmount(JsonNode? jsonNode)
    {
        return jsonNode switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
            {
                nameof(ToppingAmount.Light) => new ToppingAmount.Light(),
                nameof(ToppingAmount.Medium) => new ToppingAmount.Medium(),
                nameof(ToppingAmount.Extra) => new ToppingAmount.Extra(),
                var _ => $"'{value}' is not a valid topping amount."
            },
            _ => "Topping amount must be a string value."
        };
    }

    public static JsonObject Serialize(Topping value)
    {
        return new JsonObject
        {
            ["type"] = Serialize(value.Type),
            ["amount"] = Serialize(value.Amount)
        };
    }

    public static Validation<string, Topping> TryDeserializeTopping(JsonNode? node)
    {
        if (node is not JsonObject jsonObject)
        {
            return "Topping must be a JSON object.";
        }

        var typeValidation = jsonObject.TryGetProperty("type")
                                       .ToValidation()
                                       .Bind(TryDeserializeToppingType);

        var amountValidation = jsonObject.TryGetProperty("amount")
                                         .ToValidation()
                                         .Bind(TryDeserializeToppingAmount);

        return (typeValidation, amountValidation)
                .Apply((type, amount) => new Topping
                {
                    Type = type,
                    Amount = amount
                });
    }

    public static JsonValue Serialize(PizzaSize value)
    {
        return value switch
        {
            PizzaSize.Small => JsonValue.Create(nameof(PizzaSize.Small)),
            PizzaSize.Medium => JsonValue.Create(nameof(PizzaSize.Medium)),
            PizzaSize.Large => JsonValue.Create(nameof(PizzaSize.Large)),
            _ => throw new NotImplementedException()
        };
    }

    public static Validation<string, PizzaSize> TryDeserializePizzaSize(JsonNode? jsonNode)
    {
        return jsonNode switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
            {
                nameof(PizzaSize.Small) => new PizzaSize.Small(),
                nameof(PizzaSize.Medium) => new PizzaSize.Medium(),
                nameof(PizzaSize.Large) => new PizzaSize.Large(),
                var _ => $"'{value}' is not a valid pizza size."
            },
            _ => "Pizza size must be a string value."
        };
    }

    public static JsonObject Serialize(Pizza value)
    {
        return new JsonObject
        {
            ["size"] = Serialize(value.Size),
            ["toppings"] = value.Toppings
                                .Map(Serialize)
                                .ToJsonArray()
        };
    }

    public static Validation<string, Pizza> TryDeserializePizza(JsonNode? node)
    {
        if (node is not JsonObject jsonObject)
        {
            return "Pizza must be a JSON object.";
        }

        var sizeValidation = jsonObject.TryGetProperty("size")
                                       .ToValidation()
                                       .Bind(TryDeserializePizzaSize);

        var toppingsValidation = jsonObject.TryGetJsonObjectArrayProperty("toppings")
                                           .ToValidation()
                                           .Bind(toppingsJson => toppingsJson.Map(TryDeserializeTopping)
                                                                             .Sequence());

        return (sizeValidation, toppingsValidation)
                .Apply((size, toppings) => new Pizza
                {
                    Size = size,
                    Toppings = toppings.Freeze()
                });
    }

    public static JsonValue Serialize(OrderId value)
    {
        return JsonValue.Create(value.Value);
    }

    public static Validation<string, OrderId> TryDeserializeOrderId(JsonNode? jsonNode)
    {
        return jsonNode switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) =>
                string.IsNullOrWhiteSpace(value)
                ? "Order ID cannot be null or empty."
                : new OrderId(value),
            _ => "Order ID must be a string value."
        };
    }

    public static JsonValue Serialize(OrderStatus value)
    {
        return value switch
        {
            OrderStatus.New => JsonValue.Create(nameof(OrderStatus.New)),
            OrderStatus.Canceled => JsonValue.Create(nameof(OrderStatus.Canceled)),
            _ => throw new NotImplementedException()
        };
    }

    public static Validation<string, OrderStatus> TryDeserializeOrderStatus(JsonNode? jsonNode)
    {
        return jsonNode switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
            {
                nameof(OrderStatus.New) => new OrderStatus.New(),
                nameof(OrderStatus.Canceled) => new OrderStatus.Canceled(),
                var _ => $"'{value}' is not a valid order status."
            },
            _ => "Order status must be a string value."
        };
    }

    public static JsonObject Serialize(Order value)
    {
        return new JsonObject
        {
            ["id"] = Serialize(value.Id),
            ["status"] = Serialize(value.Status),
            ["pizzas"] = value.Pizzas
                              .Map(Serialize)
                              .ToJsonArray()
        };
    }

    public static Validation<string, Order> TryDeserializeOrder(JsonNode? node)
    {
        if (node is not JsonObject jsonObject)
        {
            return "Order must be a JSON object.";
        }

        var idValidation = jsonObject.TryGetProperty("id")
                                     .ToValidation()
                                     .Bind(TryDeserializeOrderId);

        var statusValidation = jsonObject.TryGetProperty("status")
                                         .ToValidation()
                                         .Bind(TryDeserializeOrderStatus);

        var pizzasValidation = jsonObject.TryGetJsonObjectArrayProperty("pizzas")
                                         .ToValidation()
                                         .Bind(pizzasJson => pizzasJson.Map(TryDeserializePizza)
                                                                       .Sequence());

        return (idValidation, statusValidation, pizzasValidation)
                .Apply((id, status, pizzas) => new Order
                {
                    Id = id,
                    Status = status,
                    Pizzas = pizzas.Freeze()
                });
    }
}