using Bogus;
using Bogus.DataSets;
using CsCheck;
using LanguageExt;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace common;

public static class Generator
{
    public static Gen<Randomizer> Randomizer { get; } =
        from seed in Gen.Int
        select new Randomizer(seed);

    public static Gen<Internet> Internet { get; } =
        from randomizer in Randomizer
        select new Internet { Random = randomizer };

    public static Gen<string> UserName { get; } =
        from internet in Internet
        select internet.UserName();

    public static Gen<Address> Address { get; } =
        from randomizer in Randomizer
        select new Address { Random = randomizer };

    public static Gen<Lorem> Lorem { get; } =
        from randomizer in Randomizer
        select new Lorem { Random = randomizer };

    public static Gen<Name> BogusName { get; } =
        from randomizer in Randomizer
        select new Name { Random = randomizer };

    public static Gen<Uri> AbsoluteUri { get; } =
        from internet in Internet
        select new Uri(internet.Url());

    public static Gen<Bogus.DataSets.System> BogusSystem { get; } =
        from randomizer in Randomizer
        select new Bogus.DataSets.System { Random = randomizer };

    public static Gen<DirectoryInfo> DirectoryInfo { get; } =
        from system in BogusSystem
        select new DirectoryInfo(system.FilePath());

    public static Gen<FileInfo> FileInfo { get; } =
        from system in BogusSystem
        select new FileInfo(system.FilePath());

    public static Gen<string> EmptyOrWhitespaceString { get; } =
        from whitespaceChars in Gen.OneOfConst([' ', '\t', '\n', '\r']).List[0, 10]
        select whitespaceChars switch
        {
            [] => string.Empty,
            _ => string.Concat(whitespaceChars)
        };

    public static Gen<PizzaToppingKind> PizzaToppingKind { get; } =
        Gen.OneOfConst<PizzaToppingKind>(common.PizzaToppingKind.Cheese.Instance,
                                         common.PizzaToppingKind.Pepperoni.Instance,
                                         common.PizzaToppingKind.Sausage.Instance);

    public static Gen<PizzaToppingAmount> PizzaToppingAmount { get; } =
        Gen.OneOfConst<PizzaToppingAmount>(common.PizzaToppingAmount.Light.Instance,
                                           common.PizzaToppingAmount.Normal.Instance,
                                           common.PizzaToppingAmount.Extra.Instance);

    public static Gen<PizzaSize> PizzaSize { get; } =
        Gen.OneOfConst<PizzaSize>(common.PizzaSize.Small.Instance,
                                  common.PizzaSize.Medium.Instance,
                                  common.PizzaSize.Large.Instance);

    public static Gen<Pizza> Pizza { get; } =
        from size in PizzaSize
        from toppings in Gen.Select(PizzaToppingKind, PizzaToppingAmount)
                            .FrozenSetOf((first, second) => first.Item1 == second.Item1, x => x.Item1.GetHashCode())
        select new Pizza
        {
            Size = size,
            Toppings = toppings.ToFrozenDictionary()
        };

    public static Gen<OrderId> OrderId { get; } =
        from guid in Gen.Guid
        select common.OrderId.FromOrThrow(guid.ToString());

    public static Gen<OrderStatus.Created> OrderStatusCreated { get; } =
        from date in Gen.DateTimeOffset
        from @by in UserName
        select new OrderStatus.Created
        {
            Date = date,
            By = @by
        };

    public static Gen<OrderStatus.Cancelled> OrderStatusCancelled { get; } =
        from date in Gen.DateTimeOffset
        from @by in UserName
        select new OrderStatus.Cancelled
        {
            Date = date,
            By = @by
        };

    public static Gen<OrderStatus> OrderStatus { get; } =
        Gen.OneOf<OrderStatus>(OrderStatusCreated, OrderStatusCancelled);

    public static Gen<Order> Order { get; } =
        from id in OrderId
        from status in OrderStatus
        from pizzas in Pizza.ImmutableArrayOf()
        where pizzas.Length > 0
        select new Order
        {
            Id = id,
            Status = status,
            Pizzas = pizzas
        };

    public static Gen<ETag> ETag { get; } =
        from guid in Gen.Guid
        select common.ETag.From(guid.ToString())
                          .ThrowIfFail();

    public static Gen<JsonValue> JsonValue { get; } =
        Gen.OneOf(from value in Gen.Int
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.String
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.Bool
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.Guid
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.Date
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.DateTime
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.DateTimeOffset
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.DateOnly
                  select System.Text.Json.Nodes.JsonValue.Create(value),
                  from value in Gen.TimeOnly
                  select System.Text.Json.Nodes.JsonValue.Create(value));

    public static Gen<JsonNode> JsonNode { get; } = GenerateJsonNode();

    public static Gen<JsonObject> JsonObject => GenerateJsonObject(JsonNode);

    public static Gen<JsonArray> JsonArray => GenerateJsonArray(JsonNode);

    private static Gen<JsonObject> GenerateJsonObject(Gen<JsonNode> nodeGen)
    {
        var keyGen = from key in Gen.String.AlphaNumeric
                     where !string.IsNullOrWhiteSpace(key)
                     select key;

        return from kvps in Gen.Select(keyGen, nodeGen.Null())
                               .Select(KeyValuePair.Create)
                               .Array[0, 20]
               let deduped = kvps.DistinctBy(kvp => kvp.Key.ToUpperInvariant())
               select new JsonObject(deduped);
    }

    private static Gen<JsonNode> GenerateJsonNode() =>
        Gen.Recursive<JsonNode>((depth, nodeGen) => depth < 2
                                                    ? Gen.OneOf<JsonNode>(GenerateJsonObject(nodeGen),
                                                                          GenerateJsonArray(nodeGen),
                                                                          JsonValue)
                                                    : from jsonValue in JsonValue
                                                      select jsonValue as JsonNode);

    private static Gen<JsonArray> GenerateJsonArray(Gen<JsonNode> nodeGen) =>
        from nodes in nodeGen.Array[0, 20]
        select new JsonArray(nodes);

    public static Gen<ImmutableArray<T>> ImmutableArrayOf<T>(this Gen<T> gen) =>
        from array in gen.Array
        select array.ToImmutableArray();

    public static Gen<FrozenSet<T>> FrozenSetOf<T>(this Gen<T> gen,
                                                   Func<T?, T?, bool> equals,
                                                   Func<T, int>? getHashCode = default) =>
        gen.FrozenSetOf(EqualityComparer<T>.Create(equals, getHashCode));

    public static Gen<FrozenSet<T>> FrozenSetOf<T>(this Gen<T> gen, IEqualityComparer<T>? comparer = default) =>
        from array in gen.ImmutableArrayOf()
        select array.ToFrozenSet(comparer);

    public static Gen<ImmutableArray<T>> NonEmpty<T>(this Gen<ImmutableArray<T>> array) =>
        from items in array
        where items.Length > 0
        select items;

    public static Gen<FrozenSet<T>> NonEmpty<T>(this Gen<FrozenSet<T>> set) =>
        from items in set
        where items.Count > 0
        select items;

    public static Gen<ImmutableArray<T>> SubImmutableArrayOf<T>(ICollection<T> collection) =>
        collection.Count is 0
        ? Gen.Const(ImmutableArray<T>.Empty)
        : from items in Gen.Shuffle(collection.ToArray(), collection.Count)
          select items.ToImmutableArray();

    public static Gen<FrozenSet<T>> SubFrozenSetOf<T>(ICollection<T> collection,
                                                      IEqualityComparer<T>? comparer = default)
    {
        var comparerToUse = (comparer, collection) switch
        {
            (null, FrozenSet<T> frozenSet) => frozenSet.Comparer,
            (null, ImmutableHashSet<T> hashSet) => hashSet.KeyComparer,
            _ => comparer
        };

        return collection.Count is 0
                ? Gen.Const(FrozenSet<T>.Empty)
                : from items in Gen.Shuffle(collection.ToArray(), collection.Count)
                  from length in Gen.Int[0, collection.Count]
                  select items.Take(length)
                              .ToFrozenSet(comparerToUse);
    }

    public static Gen<Option<T>> OptionOf<T>(this Gen<T> gen) =>
        Gen.Frequency((1, Gen.Const(Option<T>.None)),
                      (4, gen.Select(Option<T>.Some)));

    public static Gen<ImmutableArray<T2>> TraverseToImmutableArray<T1, T2>(ICollection<T1> collection,
                                                                           Func<T1, Gen<T2>> f) =>
        collection.Select(f)
                  .SequenceToImmutableArray();

    /// <summary>
    /// Converts a list of generators into a generator of immutable arrays.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="gens"></param>
    /// <returns></returns>
    public static Gen<ImmutableArray<T>> SequenceToImmutableArray<T>(this IEnumerable<Gen<T>> gens) =>
        gens.Aggregate(Gen.Const(ImmutableArray<T>.Empty),
                       (acc, gen) => from items in acc
                                     from item in gen
                                     select items.Add(item));

    public static Gen<FrozenSet<T2>> TraverseToFrozenSet<T1, T2>(ICollection<T1> collection,
                                                                 Func<T1, Gen<T2>> f,
                                                                 IEqualityComparer<T2>? comparer = default) =>
        collection.Select(f)
                  .SequenceToFrozenSet(comparer);

    /// <summary>
    /// Converts a list of generators into a generator of frozen sets.
    /// </summary>
    public static Gen<FrozenSet<T>> SequenceToFrozenSet<T>(this IEnumerable<Gen<T>> gens,
                                                          IEqualityComparer<T>? comparer = default) =>
        from items in gens.SequenceToImmutableArray()
        select items.ToFrozenSet(comparer);
}

public static class JsonValueGenerator
{
    public static Gen<JsonValue> Value { get; } =
        Gen.OneOf(from value in Gen.Int
                  select JsonValue.Create(value),
                  from value in Gen.String
                  select JsonValue.Create(value),
                  from value in Gen.Bool
                  select JsonValue.Create(value),
                  from value in Gen.Guid
                  select JsonValue.Create(value),
                  from value in Gen.Date
                  select JsonValue.Create(value),
                  from value in Gen.DateTime
                  select JsonValue.Create(value),
                  from value in Gen.DateTimeOffset
                  select JsonValue.Create(value),
                  from value in Gen.DateOnly
                  select JsonValue.Create(value),
                  from value in Gen.TimeOnly
                  select JsonValue.Create(value));

#pragma warning disable CA1720 // Identifier contains type name
    public static Gen<JsonValue> String { get; } =
#pragma warning restore CA1720 // Identifier contains type name
        from value in Gen.String
        select JsonValue.Create(value);

    private static Gen<JsonValue> WhereKind(this Gen<JsonValue> gen, Func<JsonValueKind, bool> predicate) =>
        gen.Where(value => predicate(value.GetValueKind()));

    private static Gen<JsonValue> WhereObject(this Gen<JsonValue> gen, Func<object, bool> predicate) =>
        gen.Where(value => predicate(value.GetValue<object>()));

    private static Gen<JsonValue> WhereObjectString(this Gen<JsonValue> gen, Func<string?, bool> predicate) =>
        gen.WhereObject(value => predicate(value.ToString()));

    public static Gen<JsonValue> NonString { get; } =
        Value.WhereKind(kind => kind is not JsonValueKind.String);

#pragma warning disable CA1720 // Identifier contains type name
    public static Gen<JsonValue> Int { get; } =
#pragma warning restore CA1720 // Identifier contains type name
        from value in Gen.Int
        select JsonValue.Create(value);

    public static Gen<JsonValue> NonInt { get; } =
        Value.WhereObject(value => value is not int and not byte);

    public static Gen<JsonValue> Bool { get; } =
        from value in Gen.OneOfConst(true, false)
        select JsonValue.Create(value);

    public static Gen<JsonValue> NonBool { get; } =
        Value.WhereKind(kind => kind is not JsonValueKind.True and not JsonValueKind.False);

#pragma warning disable CA1720 // Identifier contains type name
    public static Gen<JsonValue> Guid { get; } =
#pragma warning restore CA1720 // Identifier contains type name
        from value in Gen.Guid
        select JsonValue.Create(value);

    public static Gen<JsonValue> NonGuid { get; } =
        Value.WhereObject(value => value is not System.Guid)
             .WhereObjectString(value => !System.Guid.TryParse(value, out var _));

    public static Gen<JsonValue> AbsoluteUri { get; } =
        from value in Gen.String.AlphaNumeric
        where !string.IsNullOrWhiteSpace(value)
        let uri = new Uri($"https://{value}.com", UriKind.Absolute)
        select JsonValue.Create(uri);

    public static Gen<JsonValue> NonAbsoluteUri { get; } =
        Value.WhereObjectString(value => !Uri.TryCreate(value, UriKind.Absolute, out var _));
}

public static class JsonNodeGenerator
{
    public static Gen<JsonNode> Value { get; } = Create();

    public static Gen<JsonNode> Create() =>
        Gen.Recursive<JsonNode>((depth, nodeGen) => depth < 2
                                                    ? Gen.OneOf<JsonNode>(JsonObjectGenerator.Create(nodeGen),
                                                                          JsonArrayGenerator.Create(nodeGen),
                                                                          JsonValueGenerator.Value)
                                                    : from jsonValue in JsonValueGenerator.Value
                                                      select jsonValue as JsonNode);

    public static Gen<JsonNode> JsonObject { get; } =
        from node in JsonObjectGenerator.Create(Value)
        select node as JsonNode;

    public static Gen<JsonNode> NonJsonObject { get; } =
        Value.Where(node => node is not System.Text.Json.Nodes.JsonObject);

    public static Gen<JsonNode> JsonArray { get; } =
        from node in JsonArrayGenerator.Create(Value)
        select node as JsonNode;

    public static Gen<JsonNode> NonJsonArray { get; } =
        Value.Where(node => node is not System.Text.Json.Nodes.JsonArray);

    public static Gen<JsonNode> JsonValue { get; } =
        from node in JsonValueGenerator.Value
        select node as JsonNode;

    public static Gen<JsonNode> NonJsonValue { get; } =
        Value.Where(node => node is not System.Text.Json.Nodes.JsonValue);
}

public static class JsonArrayGenerator
{
    public static Gen<JsonArray> Value { get; } = Create(JsonNodeGenerator.Value);

    public static Gen<JsonArray> Create(Gen<JsonNode> nodeGen) =>
        from elements in nodeGen.Null().Array
        select new JsonArray(elements);
}

public static class JsonObjectGenerator
{
    public static Gen<JsonObject> Value { get; } = Create(JsonNodeGenerator.Value);

    public static Gen<JsonObject> Create(Gen<JsonNode> nodeGen) =>
        from elements in Gen.Select(Gen.String.AlphaNumeric, nodeGen.Null()).List[0, 10]
        let kvps = elements.Select(element => KeyValuePair.Create(element.Item1, element.Item2))
                           .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                           .DistinctBy(kvp => kvp.Key.ToUpperInvariant())
        select new JsonObject(kvps);
}