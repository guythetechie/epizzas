﻿using LanguageExt;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace common;

public abstract record PizzaToppingKind
{
    public sealed record Cheese : PizzaToppingKind
    {
        public static Cheese Instance { get; } = new();

        public override string ToString() => nameof(Cheese);
    }

    public sealed record Pepperoni : PizzaToppingKind
    {
        public static Pepperoni Instance { get; } = new();

        public override string ToString() => nameof(Pepperoni);
    }

    public sealed record Sausage : PizzaToppingKind
    {
        public static Sausage Instance { get; } = new();

        public override string ToString() => nameof(Sausage);
    }

    public static JsonValue Serialize(PizzaToppingKind kind) => JsonValue.Create(kind.ToString());

    public static JsonResult<PizzaToppingKind> Deserialize(JsonNode? json) =>
        from jsonValue in json.AsJsonValue()
        from value in jsonValue.AsString()
        from kind in value switch
        {
            _ when value.Equals(nameof(Pepperoni), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaToppingKind>(Pepperoni.Instance),
            _ when value.Equals(nameof(Cheese), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaToppingKind>(Cheese.Instance),
            _ when value.Equals(nameof(Sausage), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaToppingKind>(Sausage.Instance),
            _ => JsonResult.Fail<PizzaToppingKind>($"'{value}' is not a valid pizza topping kind.")
        }
        select kind;
}

public abstract record PizzaToppingAmount
{
    public sealed record Light : PizzaToppingAmount
    {
        public static Light Instance { get; } = new();

        public override string ToString() => nameof(Light);
    }

    public sealed record Normal : PizzaToppingAmount
    {
        public static Normal Instance { get; } = new();

        public override string ToString() => nameof(Normal);
    }

    public sealed record Extra : PizzaToppingAmount
    {
        public static Extra Instance { get; } = new();

        public override string ToString() => nameof(Extra);
    }

    public static JsonValue Serialize(PizzaToppingAmount amount) => JsonValue.Create(amount.ToString());

    public static JsonResult<PizzaToppingAmount> Deserialize(JsonNode? json) =>
        from jsonValue in json.AsJsonValue()
        from value in jsonValue.AsString()
        from amount in value switch
        {
            _ when value.Equals(nameof(Light), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaToppingAmount>(Light.Instance),
            _ when value.Equals(nameof(Normal), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaToppingAmount>(Normal.Instance),
            _ when value.Equals(nameof(Extra), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaToppingAmount>(Extra.Instance),
            _ => JsonResult.Fail<PizzaToppingAmount>($"'{value}' is not a valid pizza topping amount.")
        }
        select amount;
}

public abstract record PizzaSize
{
    public sealed record Small : PizzaSize
    {
        public static Small Instance { get; } = new();

        public override string ToString() => nameof(Small);
    }

    public sealed record Medium : PizzaSize
    {
        public static Medium Instance { get; } = new();

        public override string ToString() => nameof(Medium);
    }

    public sealed record Large : PizzaSize
    {
        public static Large Instance { get; } = new();

        public override string ToString() => nameof(Large);
    }

    public static JsonValue Serialize(PizzaSize size) => JsonValue.Create(size.ToString());

    public static JsonResult<PizzaSize> Deserialize(JsonNode? json) =>
        from jsonValue in json.AsJsonValue()
        from value in jsonValue.AsString()
        from size in value switch
        {
            _ when value.Equals(nameof(Small), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaSize>(Small.Instance),
            _ when value.Equals(nameof(Medium), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaSize>(Medium.Instance),
            _ when value.Equals(nameof(Large), StringComparison.OrdinalIgnoreCase) => JsonResult.Succeed<PizzaSize>(Large.Instance),
            _ => JsonResult.Fail<PizzaSize>($"'{value}' is not a valid pizza size.")
        }
        select size;
}

public sealed record Pizza
{
    public required PizzaSize Size { get; init; }
    public required FrozenDictionary<PizzaToppingKind, PizzaToppingAmount> Toppings { get; init; }

    public static JsonObject Serialize(Pizza pizza) => new()
    {
        ["size"] = PizzaSize.Serialize(pizza.Size),
        ["toppings"] = SerializeToppings(pizza.Toppings)
    };

    public static JsonArray SerializeToppings(IEnumerable<KeyValuePair<PizzaToppingKind, PizzaToppingAmount>> toppings) =>
        toppings.Select(toppings => SerializeTopping(toppings.Key, toppings.Value))
                .ToJsonArray();

    public static JsonObject SerializeTopping(PizzaToppingKind kind, PizzaToppingAmount amount) => new()
    {
        ["kind"] = PizzaToppingKind.Serialize(kind),
        ["amount"] = PizzaToppingAmount.Serialize(amount)
    };

    public static JsonResult<Pizza> Deserialize(JsonNode? json) =>
        from jsonObject in json.AsJsonObject()
        let sizeResult = from node in jsonObject.GetProperty("size")
                         from size in PizzaSize.Deserialize(node)
                         select size
        let toppingsResult = from node in jsonObject.GetProperty("toppings")
                             from toppings in DeserializeToppings(node)
                             select toppings
        from pizza in (sizeResult, toppingsResult)
                        .Apply((size, toppings) => new Pizza
                        {
                            Size = size,
                            Toppings = toppings
                        })
                        .As()
        select pizza;

    public static JsonResult<FrozenDictionary<PizzaToppingKind, PizzaToppingAmount>> DeserializeToppings(JsonNode? json) =>
        from toppingsArray in json.AsJsonArray()
        from toppingJsonObjects in toppingsArray.AsIterable()
                                                .Traverse(node => node.AsJsonObject())
                                                .As()
        from toppings in toppingJsonObjects.Traverse(DeserializeTopping).As()
        select toppings.ToFrozenDictionary();


    public static JsonResult<KeyValuePair<PizzaToppingKind, PizzaToppingAmount>> DeserializeTopping(JsonNode? json) =>
        from jsonObject in json.AsJsonObject()
        let kindResult = from property in jsonObject.GetStringProperty("kind")
                         from kind in PizzaToppingKind.Deserialize(property)
                         select kind
        let amountResult = from property in jsonObject.GetStringProperty("amount")
                           from amount in PizzaToppingAmount.Deserialize(property)
                           select amount
        from kvp in (kindResult, amountResult)
                        .Apply(KeyValuePair.Create)
                        .As()
        select kvp;
}