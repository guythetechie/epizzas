using Azure;
using LanguageExt;
using LanguageExt.Common;
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

    public static IO<Option<CosmosRecord<T>> TryReadRecord<T>(Container container, CosmosId id, PartitionKey partitionKey, Func<JsonObject, T> recordDeserializer) =>
        OptionT<IO, JsonObject>.LiftIO(TryReadItem(container, id, partitionKey))
                .Map(json => DeserializeRecord(json, recordDeserializer, _ => partitionKey))
                .Run()
                .As();

    public static IO<Option<JsonObject>> TryReadItem(Container container, CosmosId id, PartitionKey partitionKey) =>
        from response in IO.liftAsync(async env => await container.ReadItemStreamAsync(id.Value, partitionKey, cancellationToken: env.Token)).Use()
        from json in response.StatusCode switch
        {
            HttpStatusCode.NotFound => IO.Pure(Option<JsonObject>.None),
            _ => from _ in IO.Pure(response.EnsureSuccessStatusCode())
                 from json in GetContentAsJsonObject(response)
                 select Prelude.Some(json)
        }
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

    public static Eff<CosmosId> TryGetCosmosId(JsonObject jsonObject) =>
        jsonObject.TryGetStringProperty("id")
                  .Map(id => new CosmosId(id))
                  .ToEff(Error.New);

    public static Eff<ETag> TryGetETag(JsonObject jsonObject) =>
        jsonObject.TryGetStringProperty("_etag")
                  .Map(etag => new ETag(etag))
                  .ToEff(Error.New);

    public static Eff<Seq<JsonObject>> GetQueryResults(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        from iterator in GetFeedIterator(container, cosmosQueryOptions)
        from results in GetQueryResults(iterator)
        select results;

    private static Eff<FeedIterator> GetFeedIterator(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        Prelude.liftEff(() =>
        {
            var queryDefinition = cosmosQueryOptions.Query;
            var continuationToken = cosmosQueryOptions.ContinuationToken.ValueUnsafe()?.Value;

            var queryRequestOptions = new QueryRequestOptions();
            cosmosQueryOptions.PartitionKey.Iter(partitionKey => queryRequestOptions.PartitionKey = partitionKey);

            return container.GetItemQueryStreamIterator(queryDefinition, continuationToken, queryRequestOptions);
        });

    private static Eff<Seq<JsonObject>> GetQueryResults(FeedIterator iterator)
    {
        Eff<Seq<JsonObject>> getSeq(Seq<JsonObject> seq) =>
            from results in iterator.HasMoreResults
                            ? from currentPageResults in GetCurrentPageResults(iterator)
                              from nextSeq in getSeq(seq + currentPageResults.Documents)
                              select nextSeq
                            : Prelude.SuccessEff(seq)
            select results;

        return getSeq([]);
    }

    private static Eff<(Seq<JsonObject> Documents, Option<ContinuationToken> ContinuationToken)> GetCurrentPageResults(FeedIterator iterator) =>
        from cancellationToken in Prelude.cancelTokenEff
        from response in Utilities.UseEff(async () => await iterator.ReadNextAsync(cancellationToken))
        let _ = response.EnsureSuccessStatusCode()
        from responseJson in GetContentAsJsonObject(response)
        let documents = responseJson.GetJsonArrayProperty("Documents")
                                    .GetJsonObjects()
        let continuationToken = string.IsNullOrWhiteSpace(response.ContinuationToken)
                                ? Option<ContinuationToken>.None
                                : new ContinuationToken(response.ContinuationToken)
        select (documents, continuationToken);

    public static Eff<Either<CosmosError.ResourceAlreadyExists, Unit>> CreateRecord(Container container, JsonObject jsonObject, PartitionKey partitionKey) =>
        from response in Utilities.UseEff(() => CreateItem(container, jsonObject, partitionKey))
        let _ = response.EnsureSuccessStatusCode()
        select response.StatusCode switch
        {
            HttpStatusCode.Conflict => Either<CosmosError.ResourceAlreadyExists, Unit>.Left(CosmosError.ResourceAlreadyExists.Instance),
            HttpStatusCode.PreconditionFailed => CosmosError.ResourceAlreadyExists.Instance,
            _ => Prelude.unit
        };

    private static Eff<ResponseMessage> CreateItem(Container container, JsonObject jsonObject, PartitionKey partitionKey) =>
        from stream in Prelude.use(() => Serializer.ToStream(jsonObject))
        let options = new ItemRequestOptions { IfNoneMatchEtag = "*" }
        from cancellationToken in Prelude.cancelTokenEff
        from response in Prelude.liftEff(async () => await container.CreateItemStreamAsync(stream, partitionKey, options, cancellationToken: cancellationToken))
        select response;
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