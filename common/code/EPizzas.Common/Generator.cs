using EPizzas.Common;
using FsCheck;
using FsCheck.Fluent;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace EPizzas.Common;

public static class Generator
{
    public static Gen<string> GuidString { get; } =
        from guid in GenerateDefault<Guid>()
        select guid.ToString();

    public static Gen<string> WhiteSpaceString { get; } =
        Gen.OneOf(Gen.Constant(string.Empty),
                  GenerateDefault<char>()
                               .Where(char.IsWhiteSpace)
                               .ArrayOf()
                               .Select(string.Concat));

    public static Gen<string> AlphaNumericString { get; } =
        GenerateDefault<char>().Where(char.IsLetterOrDigit)
                               .ListOf()
                               .Select(string.Concat);

    public static Gen<string> NonEmptyOrWhiteSpaceString { get; } =
        GenerateDefault<NonWhiteSpaceString>()
                     .Select(x => x.Item)
                     .Where(x => string.IsNullOrWhiteSpace(x) is false);

    public static Gen<JsonNode> JsonNode { get; } = GenerateJsonNode();

    public static Gen<JsonObject> JsonObject { get; } = GenerateJsonObject();

    public static Gen<JsonValue> JsonValue { get; } = GenerateJsonValue();

    public static Gen<JsonArray> JsonArray { get; } = GenerateJsonArray();

    private static Gen<JsonValue> GenerateJsonValue()
    {
        return Gen.OneOf(GenerateJsonValue<bool>(),
                         GenerateJsonValue<byte>(),
                         GenerateJsonValue<char>(),
                         GenerateJsonValue<DateTime>(),
                         GenerateJsonValue<DateTimeOffset>(),
                         GenerateJsonValue<decimal>(),
                         GenerateJsonValue<double>(),
                         GenerateJsonValue<Guid>(),
                         GenerateJsonValue<short>(),
                         GenerateJsonValue<int>(),
                         GenerateJsonValue<long>(),
                         GenerateJsonValue<sbyte>(),
                         GenerateJsonValue<float>(),
                         GenerateJsonValue<string>(),
                         GenerateJsonValue<ushort>(),
                         GenerateJsonValue<uint>(),
                         GenerateJsonValue<ulong>());
    }

    public static Gen<JsonValue> GenerateJsonValue<T>()
    {
        var generator = typeof(T) switch
        {
            var type when type == typeof(double) => GenerateDefault<double>()
                                                                 .Where(double.IsFinite)
                                                                 .Where(d => double.IsNaN(d) is false)
                                                                 .Select(d => (T)(object)d),
            var type when type == typeof(float) => GenerateDefault<float>()
                                                                .Where(float.IsFinite)
                                                                .Where(f => float.IsNaN(f) is false)
                                                                .Select(f => (T)(object)f),
            _ => GenerateDefault<T>()
        };

        return generator.Select(t => System.Text.Json.Nodes.JsonValue.Create(t)!);
    }

    private static Gen<JsonNode> GenerateJsonNode()
    {
        return Gen.Sized(GenerateJsonNode);
    }

    private static Gen<JsonNode> GenerateJsonNode(int size)
    {
        return size < 1
                ? GenerateJsonValue().Select(value => value as JsonNode)
                : Gen.OneOf(from jsonValue in GenerateJsonValue()
                            select jsonValue as JsonNode,
                            from jsonObject in GenerateJsonObject(GenerateJsonNode(size / 5))
                            select jsonObject as JsonNode,
                            from jsonArray in GenerateJsonArray(GenerateJsonNode(size / 5))
                            select jsonArray as JsonNode);
    }

    private static Gen<JsonObject> GenerateJsonObject()
    {
        return GenerateJsonObject(GenerateJsonNode());
    }

    private static Gen<JsonObject> GenerateJsonObject(Gen<JsonNode> nodeGenerator)
    {
        return NonEmptyOrWhiteSpaceString
                        .ListOf()
                        .Select(list => list.Distinct())
                        .SelectMany(list => Gen.CollectToSequence(list,
                                                                    key => from node in nodeGenerator.OrNull()
                                                                           select KeyValuePair.Create(key, node)))
                        .Select(list => new JsonObject(list));
    }

    private static Gen<JsonArray> GenerateJsonArray()
    {
        return GenerateJsonArray(GenerateJsonNode());
    }

    private static Gen<JsonArray> GenerateJsonArray(Gen<JsonNode> nodeGenerator)
    {
        return nodeGenerator.OrNull()
                            .ArrayOf()
                            .Select(array => new JsonArray(array));
    }

    public static Gen<T> GenerateDefault<T>()
    {
        return ArbMap.Default.GeneratorFor<T>();
    }
}

public static class GenExtensions
{
    public static Gen<Seq<T>> SeqOf<T>(this Gen<T> gen)
    {
        return gen.ListOf().Select(list => list.ToSeq());
    }

    public static Gen<Seq<T>> SeqOf<T>(this Gen<T> gen, uint minimum, uint maximum)
    {
        if (minimum > maximum)
        {
            throw new InvalidOperationException("Minimum cannot be greater than maximum.");
        }

        return from count in Gen.Choose((int)minimum, (int)maximum)
               from list in gen.ListOf(count)
               select list.ToSeq();
    }

    public static Gen<T> Elements<T>(this Gen<Seq<T>> gen)
    {
        return gen.Select(x => x.AsEnumerable())
                  .SelectMany(x => Gen.Elements(x));
    }

    public static Gen<Seq<T>> NonEmptySeqOf<T>(this Gen<T> gen)
    {
        return gen.NonEmptyListOf().Select(list => list.ToSeq());
    }

    public static Gen<Seq<T>> SubSeqOf<T>(IEnumerable<T> items)
    {
        return Gen.Constant(items)
                  .Select(items => items.ToSeq())
                  .SelectMany(items => items.IsEmpty
                                        ? Gen.Constant(Seq<T>.Empty)
                                        : Gen.SubListOf(items.AsEnumerable())
                                             .Select(list => list.ToSeq()));
    }

    public static Gen<Seq<T>> DistinctBy<T, TKey>(this Gen<Seq<T>> gen, Func<T, TKey> keySelector)
    {
        return gen.Select(items => items.DistinctBy(keySelector).ToSeq());
    }

    public static Gen<Option<T>> OptionOf<T>(this Gen<T> gen)
    {
        return Gen.OneOf(gen.Select(t => Option<T>.Some(t)), Gen.Constant(Option<T>.None));
    }

    public static T Sample<T>(this Gen<T> gen)
    {
        return gen.Sample(1).Head();
    }

    public static Gen<T> MapFilter<T>(this Gen<T> gen, Func<T, T> map, Func<T, bool> filter)
    {
        return gen.ToArbitrary().MapFilter(map, filter).Generator;
    }

    public static Gen<Seq<T2>> CollectToSeq<T1, T2>(this Gen<Seq<T1>> gen, Func<T1, Gen<T2>> f)
    {
        return gen.SelectMany(enumerable => Gen.CollectToSequence(enumerable, f))
                  .Select(enumerable => enumerable.ToSeq());
    }
}