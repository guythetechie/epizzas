using CommunityToolkit.Diagnostics;
using EPizzas.Common;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EPizzas.Ordering.Api.V1;

[JsonConverter(typeof(Converter))]
public abstract record ToppingType
{
    public sealed record Pepperoni : ToppingType;
    public sealed record Ham : ToppingType;
    public sealed record Cheese : ToppingType;

    public sealed override string ToString()
    {
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
        return this switch
        {
            Pepperoni => nameof(Pepperoni),
            Ham => nameof(Ham),
            Cheese => nameof(Cheese),
            _ => throw new NotImplementedException()
        };
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
    }

    internal sealed class Converter : JsonConverter<ToppingType>
    {
        public override ToppingType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, ToppingType value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, ToppingType> Deserialize(JsonNode? jsonNode)
        {
            return jsonNode switch
            {
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
                {
                    nameof(Pepperoni) => new Pepperoni(),
                    nameof(Ham) => new Ham(),
                    nameof(Cheese) => new Cheese(),
                    var _ => $"'{value}' is not a valid topping type."
                },
                _ => "Topping type must be a string value."
            };
        }

        public static JsonValue Serialize(ToppingType value)
        {
            return JsonValue.Create(value.ToString());
        }
    }
}

[JsonConverter(typeof(Converter))]
public abstract record ToppingAmount
{
    public sealed record Light : ToppingAmount;
    public sealed record Medium : ToppingAmount;
    public sealed record Extra : ToppingAmount;

    public sealed override string ToString()
    {
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
        return this switch
        {
            Light => nameof(Light),
            Medium => nameof(Medium),
            Extra => nameof(Extra),
            _ => throw new NotImplementedException()
        };
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
    }

    internal sealed class Converter : JsonConverter<ToppingAmount>
    {
        public override ToppingAmount Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, ToppingAmount value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, ToppingAmount> Deserialize(JsonNode? jsonNode)
        {
            return jsonNode switch
            {
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
                {
                    nameof(Light) => new Light(),
                    nameof(Medium) => new Medium(),
                    nameof(Extra) => new Extra(),
                    var _ => $"'{value}' is not a valid topping amount."
                },
                _ => "Topping type must be a string value."
            };
        }

        public static JsonValue Serialize(ToppingAmount value)
        {
            return JsonValue.Create(value.ToString());
        }
    }
}

[JsonConverter(typeof(Converter))]
public sealed record Topping
{
    public required ToppingType Type { get; init; }
    public required ToppingAmount Amount { get; init; }

    internal sealed class Converter : JsonConverter<Topping>
    {
        public override Topping Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, Topping value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, Topping> Deserialize(JsonNode? node)
        {
            if (node is not JsonObject jsonObject)
            {
                return "Topping must be a JSON object.";
            }

            var typeValidation = jsonObject.TryGetProperty("type")
                                           .ToValidation()
                                           .Bind(ToppingType.Converter.Deserialize);

            var amountValidation = jsonObject.TryGetProperty("amount")
                                             .ToValidation()
                                             .Bind(ToppingAmount.Converter.Deserialize);

            return (typeValidation, amountValidation)
                    .Apply((type, amount) => new Topping
                    {
                        Type = type,
                        Amount = amount
                    });
        }

        public static JsonObject Serialize(Topping value)
        {
            return new JsonObject
            {
                ["type"] = ToppingType.Converter.Serialize(value.Type),
                ["amount"] = ToppingAmount.Converter.Serialize(value.Amount)
            };
        }
    }
}

[JsonConverter(typeof(Converter))]
public abstract record PizzaSize
{
    public sealed record Small : PizzaSize;
    public sealed record Medium : PizzaSize;
    public sealed record Large : PizzaSize;

    public sealed override string ToString()
    {
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
        return this switch
        {
            Small => nameof(Small),
            Medium => nameof(Medium),
            Large => nameof(Large),
            _ => throw new NotImplementedException()
        };
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
    }

    internal sealed class Converter : JsonConverter<PizzaSize>
    {
        public override PizzaSize Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, PizzaSize value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, PizzaSize> Deserialize(JsonNode? jsonNode)
        {
            return jsonNode switch
            {
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
                {
                    nameof(Small) => new Small(),
                    nameof(Medium) => new Medium(),
                    nameof(Large) => new Large(),
                    _ => $"'{value}' is not a valid pizza size."
                },
                _ => "Pizza size must be a string value."
            };
        }

        public static JsonValue Serialize(PizzaSize value)
        {
            return JsonValue.Create(value.ToString());
        }
    }
}

[JsonConverter(typeof(Converter))]
public sealed record Pizza
{
    public required PizzaSize Size { get; init; }
    public required IReadOnlyList<Topping> Toppings { get; init; }

    internal sealed class Converter : JsonConverter<Pizza>
    {
        public override Pizza Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, Pizza value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, Pizza> Deserialize(JsonNode? node)
        {
            if (node is not JsonObject jsonObject)
            {
                return "Pizza must be a JSON object.";
            }

            var sizeValidation = jsonObject.TryGetProperty("size")
                                           .ToValidation()
                                           .Bind(PizzaSize.Converter.Deserialize);

            var toppingsValidation = jsonObject.TryGetJsonObjectArrayProperty("toppings")
                                               .ToValidation()
                                               .Bind(toppingsJson => toppingsJson.Map(Topping.Converter.Deserialize)
                                                                                 .Sequence());

            return (sizeValidation, toppingsValidation)
                    .Apply((size, toppings) => new Pizza
                    {
                        Size = size,
                        Toppings = toppings.Freeze()
                    });
        }

        public static JsonObject Serialize(Pizza value)
        {
            return new JsonObject
            {
                ["size"] = PizzaSize.Converter.Serialize(value.Size),
                ["toppings"] = value.Toppings
                                    .Map(Topping.Converter.Serialize)
                                    .ToJsonArray()
            };
        }
    }
}

[JsonConverter(typeof(Converter))]
public sealed record OrderId
{
    public OrderId(string value)
    {
        Guard.IsNotNullOrWhiteSpace(value, nameof(value));

        Value = value;
    }

    public string Value { get; }

    internal sealed class Converter : JsonConverter<OrderId>
    {
        public override OrderId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, OrderId value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, OrderId> Deserialize(JsonNode? jsonNode)
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

        public static JsonValue Serialize(OrderId value)
        {
            return JsonValue.Create(value.ToString());
        }
    }
}

[JsonConverter(typeof(Converter))]
public abstract record OrderStatus
{
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public sealed record New : OrderStatus;
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    public sealed record Canceled : OrderStatus;

    public sealed override string ToString()
    {
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
        return this switch
        {
            New => nameof(New),
            Canceled => nameof(Canceled),
            _ => throw new NotImplementedException()
        };
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
    }

    internal sealed class Converter : JsonConverter<OrderStatus>
    {
        public override OrderStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, OrderStatus value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, OrderStatus> Deserialize(JsonNode? jsonNode)
        {
            return jsonNode switch
            {
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out var value) => value switch
                {
                    nameof(New) => new New(),
                    nameof(Canceled) => new Canceled(),
                    _ => $"'{value}' is not a valid order status."
                },
                _ => "Order status must be a string value."
            };
        }

        public static JsonValue Serialize(OrderStatus value)
        {
            return JsonValue.Create(value.ToString());
        }
    }
}

[JsonConverter(typeof(Converter))]
public sealed record Order
{
    private readonly Seq<Pizza> pizzas;

    public required OrderId Id { get; init; }

    public IReadOnlyList<Pizza> Pizzas
    {
        get => pizzas.Freeze();
        init => pizzas = ValidatePizzas(value).ToSeq();
    }

    public required OrderStatus Status { get; init; }

    private static IReadOnlyList<Pizza> ValidatePizzas(IReadOnlyList<Pizza> pizzas)
    {
        Guard.IsNotEmpty(pizzas.Freeze(), nameof(pizzas));
        return pizzas;
    }

    internal sealed class Converter : JsonConverter<Order>
    {
        public override Order Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader);

            return Deserialize(node).IfFailThrowJsonException();
        }

        public override void Write(Utf8JsonWriter writer, Order value, JsonSerializerOptions options)
        {
            Serialize(value).WriteTo(writer, options);
        }

        public static Validation<string, Order> Deserialize(JsonNode? node)
        {
            if (node is not JsonObject jsonObject)
            {
                return "Order must be a JSON object.";
            }

            var idValidation = jsonObject.TryGetProperty("id")
                                         .ToValidation()
                                         .Bind(OrderId.Converter.Deserialize);

            var statusValidation = jsonObject.TryGetProperty("status")
                                             .ToValidation()
                                             .Bind(OrderStatus.Converter.Deserialize);

            var pizzasValidation = jsonObject.TryGetJsonObjectArrayProperty("pizzas")
                                             .ToValidation()
                                             .Bind(pizzasJson => pizzasJson.Map(Pizza.Converter.Deserialize)
                                                                           .Sequence());

            return (idValidation, statusValidation, pizzasValidation)
                    .Apply((id, status, pizzas) => new Order
                    {
                        Id = id,
                        Status = status,
                        Pizzas = pizzas.Freeze()
                    });
        }

        public static JsonObject Serialize(Order value)
        {
            return new JsonObject
            {
                ["id"] = OrderId.Converter.Serialize(value.Id),
                ["status"] = OrderStatus.Converter.Serialize(value.Status),
                ["pizzas"] = value.Pizzas
                                  .Map(Pizza.Converter.Serialize)
                                  .ToJsonArray()
            };
        }
    }
}