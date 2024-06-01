using Azure;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Azure.Cosmos;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace common;

public sealed record CosmosId : NonEmptyString
{
    public CosmosId(string value) : base(value) { }

    public static CosmosId Generate() =>
        new(Guid.NewGuid().ToString());
}

public sealed record CosmosRecord<T>
{
    public required CosmosId Id { get; init; }

    public required PartitionKey PartitionKey { get; init; }

    public required T Record { get; init; }

    public required ETag ETag { get; init; }
}

public sealed record CosmosQueryOptions
{
    public required QueryDefinition Query { get; init; }
    public Option<ContinuationToken> ContinuationToken { get; init; } = Option<ContinuationToken>.None;
    public Option<PartitionKey> PartitionKey { get; init; } = Option<PartitionKey>.None;
}

public abstract record CosmosError
{
    public sealed record ResourceAlreadyExists : CosmosError
    {
        public static ResourceAlreadyExists Instance { get; } = new();
    }
}

public static class CosmosModule
{
    public static CosmosSerializer Serializer { get; } = new CosmosSystemTextJsonSerializer();

    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static OptionT<IO, CosmosRecord<T>> TryReadRecord<T>(Container container, CosmosId id, PartitionKey partitionKey, Func<JsonObject, T> recordDeserializer) =>
        TryReadItem(container, id, partitionKey)
            .Map(json => DeserializeRecord(json, recordDeserializer, _ => partitionKey));

    public static OptionT<IO, JsonObject> TryReadItem(Container container, CosmosId id, PartitionKey partitionKey) =>
        from response in OptionTIO.Lift(async env => await container.ReadItemStreamAsync(id.Value, partitionKey, cancellationToken: env.Token))
        from _ in OptionTIO.Use(response)
        from json in OptionTIO.Lift(response.StatusCode switch
        {
            HttpStatusCode.NotFound => IO.Pure(Option<JsonObject>.None),
            _ => from response in IO.Pure(response.EnsureSuccessStatusCode())
                 from json in GetContentAsJsonObject(response)
                 select Prelude.Some(json)
        })
        select json;

    private static IO<JsonObject> GetContentAsJsonObject(ResponseMessage response) =>
        from node in GetJsonContent(response)
        select node is JsonObject jsonObject
            ? jsonObject
            : throw new InvalidOperationException("Cosmos response content is not a JSON object.");

    private static IO<JsonNode> GetJsonContent(ResponseMessage response) =>
        IO.liftAsync(async env =>
        {
            using var stream = response.Content;

            return await JsonSerializer.DeserializeAsync<JsonNode>(stream, jsonSerializerOptions, env.Token)
                    ?? throw new InvalidOperationException("Could not deserialize Cosmos response.");
        });

    private static CosmosRecord<T> DeserializeRecord<T>(JsonObject jsonObject, Func<JsonObject, T> recordDeserializer, Func<T, PartitionKey> getPartitionKey)
    {
        var record = recordDeserializer(jsonObject);

        return new CosmosRecord<T>
        {
            ETag = GetETag(jsonObject),
            Id = GetCosmosId(jsonObject),
            PartitionKey = getPartitionKey(record),
            Record = record
        };
    }

    public static CosmosId GetCosmosId(JsonObject jsonObject)
    {
        var idString = jsonObject.GetStringProperty("id");
        return new CosmosId(idString);
    }

    public static ETag GetETag(JsonObject jsonObject)
    {
        var eTagString = jsonObject.GetStringProperty("_etag");
        return new ETag(eTagString);
    }

    public static IO<Seq<JsonObject>> GetQueryResults(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        from iterator in GetFeedIterator(container, cosmosQueryOptions)
        from results in GetQueryResults(iterator)
        select results;

    private static IO<FeedIterator> GetFeedIterator(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        IO.lift(() =>
        {
            var queryDefinition = cosmosQueryOptions.Query;
            var continuationToken = cosmosQueryOptions.ContinuationToken.ValueUnsafe()?.Value;

            var queryRequestOptions = new QueryRequestOptions();
            cosmosQueryOptions.PartitionKey.Iter(partitionKey => queryRequestOptions.PartitionKey = partitionKey);

            return container.GetItemQueryStreamIterator(queryDefinition, continuationToken, queryRequestOptions);
        });

    private static IO<Seq<JsonObject>> GetQueryResults(FeedIterator iterator)
    {
        IO<Seq<JsonObject>> getSeq(Seq<JsonObject> seq) =>
            from results in iterator.HasMoreResults
                            ? from currentPageResults in GetCurrentPageResults(iterator)
                              let currentPageDocuments = currentPageResults.Documents
                              from nextSeq in getSeq(seq + currentPageResults.Documents)
                              select nextSeq
                            : IO.Pure(seq)
            select results;

        return getSeq(Seq<JsonObject>.Empty);
    }
    //IO.lift(() =>
    //{
    //    var seq = Seq<JsonObject>.Empty;

    //    while (iterator.HasMoreResults)
    //    {
    //        seq = seq + GetCurrentPageResults(iterator).Run().Documents;
    //    }

    //    return seq;
    //});

    private static IO<(Seq<JsonObject> Documents, Option<ContinuationToken> ContinuationToken)> GetCurrentPageResults(FeedIterator iterator) =>
        from response in IO.liftAsync(async env => await iterator.ReadNextAsync(env.Token))
        from _ in Prelude.use(() => response)
        let __ = response.EnsureSuccessStatusCode()
        from responseJson in GetContentAsJsonObject(response)
        let documents = responseJson.GetJsonArrayProperty("Documents")
                                    .GetJsonObjects()
        let continuationToken = string.IsNullOrWhiteSpace(response.ContinuationToken)
                                ? Option<ContinuationToken>.None
                                : new ContinuationToken(response.ContinuationToken)
        select (documents, continuationToken);

    public static EitherT<CosmosError.ResourceAlreadyExists, IO, Unit> CreateRecord(Container container, JsonObject jsonObject, PartitionKey partitionKey) =>
        from response in EitherTIO.Lift<CosmosError.ResourceAlreadyExists, ResponseMessage>(async env =>
        {
            using var recordStream = Serializer.ToStream(jsonObject);
            var options = new ItemRequestOptions { IfNoneMatchEtag = "*" };

            return await container.CreateItemStreamAsync(recordStream, partitionKey, options, env.Token);
        })
        from _ in EitherTIO.Use<CosmosError.ResourceAlreadyExists, ResponseMessage>(response)
        from unit in response.StatusCode switch
        {
            HttpStatusCode.Conflict => EitherTIO.Left<CosmosError.ResourceAlreadyExists, Unit>(CosmosError.ResourceAlreadyExists.Instance),
            HttpStatusCode.PreconditionFailed => EitherTIO.Left<CosmosError.ResourceAlreadyExists, Unit>(CosmosError.ResourceAlreadyExists.Instance),
            _ => from _ in EitherTIO.Right<CosmosError.ResourceAlreadyExists, ResponseMessage>(response.EnsureSuccessStatusCode())
                 select Prelude.unit
        }
        select unit;
}

file sealed class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    public override T FromStream<T>(Stream stream)
    {
        return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidOperationException("Failed to deserialize JSON.");
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input);
        stream.Position = 0;
        return stream;
    }
}