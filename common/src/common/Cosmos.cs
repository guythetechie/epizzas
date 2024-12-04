using Azure;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace common;

public sealed record CosmosId
{
    private readonly string value;

    private CosmosId(string value) => this.value = value;

    public override string ToString() => value;

    public static Fin<CosmosId> From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Error.New("Cosmos ID cannot be null or whitespace.")
            : new CosmosId(value);

    public static CosmosId FromOrThrow(string value) =>
        From(value).ThrowIfFail();

    public static CosmosId Generate() =>
        new(Guid.CreateVersion7().ToString());
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

public record CosmosError : Expected
{
    protected CosmosError(string Message, int Code) : base(Message, Code, default) { }

    public sealed record ResourceAlreadyExists : CosmosError
    {
        private ResourceAlreadyExists() : base("Resource already exists.", 409)
        {
        }

        public static ResourceAlreadyExists Instance { get; } = new();
    }

    public sealed record ETagMismatch : CosmosError
    {
        private ETagMismatch() : base("ETag mismatch.", 412)
        {
        }

        public static ETagMismatch Instance { get; } = new();
    }
}

public static class CosmosModule
{
    public static CosmosSerializer Serializer { get; } = new CosmosSystemTextJsonSerializer();

    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static Eff<Option<CosmosRecord<T>>> ReadRecord<T>(Container container, CosmosId id, PartitionKey partitionKey, Func<JsonObject, JsonResult<T>> deserializeRecord) =>
        from itemOption in ReadItem(container, id, partitionKey)
        from recordOption in itemOption.Traverse(json => DeserializeRecord(json, deserializeRecord, _ => partitionKey).ToEff())
        select recordOption;

    public static Eff<Option<JsonObject>> ReadItem(Container container, CosmosId id, PartitionKey partitionKey) =>
        from response in IO.liftAsync(async env => await container.ReadItemStreamAsync(id.ToString(), partitionKey, cancellationToken: env.Token))
        from json in response.StatusCode switch
        {
            HttpStatusCode.NotFound => IO.pure(Option<JsonObject>.None),
            _ => from _ in IO.lift(() => response.EnsureSuccessStatusCode())
                 from json in GetContentAsJsonObject(response).RunIO()
                 select Prelude.Some(json)
        }
        select json;

    private static Eff<JsonObject> GetContentAsJsonObject(ResponseMessage response) =>
        from node in GetJsonContent(response)
        from jsonObject in node.AsJsonObject().ToEff()
        select jsonObject;

    private static Eff<JsonNode> GetJsonContent(ResponseMessage response) =>
        IO.liftAsync(async env =>
        {
            using var stream = response.Content;

            return await JsonSerializer.DeserializeAsync<JsonNode>(stream, jsonSerializerOptions, env.Token)
                    ?? throw new InvalidOperationException("Could not deserialize Cosmos response.");
        });

    private static JsonResult<CosmosRecord<T>> DeserializeRecord<T>(JsonObject jsonObject, Func<JsonObject, JsonResult<T>> deserializeRecord, Func<T, PartitionKey> getPartitionKey) =>
        from id in GetCosmosId(jsonObject)
        from eTag in GetETag(jsonObject)
        from record in deserializeRecord(jsonObject)
        select new CosmosRecord<T>
        {
            Id = id,
            ETag = eTag,
            PartitionKey = getPartitionKey(record),
            Record = record
        };

    public static JsonResult<CosmosId> GetCosmosId(JsonObject jsonObject) =>
        from id in jsonObject.GetStringProperty("id")
        from cosmosId in JsonResult.Lift(CosmosId.From(id))
        select cosmosId;

    public static JsonResult<ETag> GetETag(JsonObject jsonObject) =>
        from eTag in jsonObject.GetStringProperty("_etag")
        select new ETag(eTag);

    public static Eff<ImmutableArray<JsonObject>> GetQueryResults(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        from iterator in GetFeedIterator(container, cosmosQueryOptions)
        from results in GetQueryResults(iterator)
        select results;

    private static Eff<FeedIterator> GetFeedIterator(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        Prelude.liftEff(() =>
        {
            var queryDefinition = cosmosQueryOptions.Query;
            var continuationToken = cosmosQueryOptions.ContinuationToken.ValueUnsafe()?.ToString();

            var queryRequestOptions = new QueryRequestOptions();
            cosmosQueryOptions.PartitionKey.Iter(partitionKey => queryRequestOptions.PartitionKey = partitionKey);

            return container.GetItemQueryStreamIterator(queryDefinition, continuationToken, queryRequestOptions);
        });

    private static Eff<ImmutableArray<JsonObject>> GetQueryResults(FeedIterator iterator)
    {
        Eff<ImmutableArray<JsonObject>> getResults(ImmutableArray<JsonObject> jsonObjects) =>
            from results in iterator.HasMoreResults
                            ? from currentPageResults in GetCurrentPageResults(iterator)
                              from nextImmutableArray in getResults([.. jsonObjects, .. currentPageResults.Documents])
                              select nextImmutableArray
                            : Prelude.SuccessEff(jsonObjects)
            select results;

        return getResults([]);
    }

    private static Eff<(ImmutableArray<JsonObject> Documents, Option<ContinuationToken> ContinuationToken)> GetCurrentPageResults(FeedIterator iterator) =>
        from cancellationToken in Prelude.cancelTokenEff
        from x in Prelude.liftEff(async () =>
        {
            using var response = await iterator.ReadNextAsync(cancellationToken);

            response.EnsureSuccessStatusCode();

            var continuationToken = ContinuationToken.From(response.ContinuationToken).ToOption();
            var responseJsonFin = await GetContentAsJsonObject(response).RunAsync(EnvIO.New(token: cancellationToken));

            return from responseJson in responseJsonFin
                   select (responseJson, continuationToken);
        })
        let responseJson = x.responseJson
        let continuationToken = x.continuationToken
        from documentsJsonArray in responseJson.GetJsonArrayProperty("Documents").ToEff()
        from documents in documentsJsonArray.GetJsonObjects().ToEff()
        select (documents, continuationToken);

    public static Eff<Either<CosmosError.ResourceAlreadyExists, Unit>> CreateRecord(Container container, JsonObject jsonObject, PartitionKey partitionKey) =>
        from cancellationToken in Prelude.cancelTokenEff
        from result in Prelude.liftEff(async () =>
        {
            using var stream = Serializer.ToStream(jsonObject);
            var options = new ItemRequestOptions { IfNoneMatchEtag = "*" };

            using var response = await container.CreateItemStreamAsync(stream, partitionKey, options, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed:
                    return Either<CosmosError.ResourceAlreadyExists, Unit>.Left(CosmosError.ResourceAlreadyExists.Instance);
                default:
                    response.EnsureSuccessStatusCode();
                    return Unit.Default;
            }
        })
        select result;

    public static Eff<Either<CosmosError.ETagMismatch, Unit>> PatchRecord(Container container, CosmosId id, PartitionKey partitionKey, IEnumerable<PatchOperation> patchOperations, ETag eTag) =>
        from cancellationToken in Prelude.cancelTokenEff
        from result in Prelude.liftEff(async () =>
        {
            using var response = await container.PatchItemStreamAsync(id.ToString(), partitionKey, [.. patchOperations], new PatchItemRequestOptions { IfMatchEtag = eTag.ToString() }, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.PreconditionFailed:
                    return Either<CosmosError.ETagMismatch, Unit>.Left(CosmosError.ETagMismatch.Instance);
                default:
                    response.EnsureSuccessStatusCode();
                    return Unit.Default;
            }
        })
        select result;

    public static Eff<Either<CosmosError.ETagMismatch, Unit>> DeleteRecord(Container container, CosmosId id, PartitionKey partitionKey, ETag eTag) =>
        from cancellationToken in Prelude.cancelTokenEff
        from result in Prelude.liftEff(async () =>
        {
            using var response = await container.DeleteItemStreamAsync(id.ToString(), partitionKey, new ItemRequestOptions { IfMatchEtag = eTag.ToString() }, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.PreconditionFailed:
                    return Either<CosmosError.ETagMismatch, Unit>.Left(CosmosError.ETagMismatch.Instance);
                default:
                    response.EnsureSuccessStatusCode();
                    return Unit.Default;
            }
        })
        select result;
}

file sealed class CosmosSystemTextJsonSerializer : CosmosSerializer
{
    public override T FromStream<T>(Stream stream) =>
        JsonSerializer.Deserialize<T>(stream, JsonSerializerOptions.Web) ?? throw new InvalidOperationException("Failed to deserialize JSON.");

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, JsonSerializerOptions.Web);
        stream.Position = 0;
        return stream;
    }
}