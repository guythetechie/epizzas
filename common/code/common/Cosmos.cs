using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Azure.Cosmos;
using OpenTelemetry;
using static LanguageExt.Prelude;

namespace common;

public sealed record CosmosId
{
    private readonly string value;

    private CosmosId(string value) => this.value = value;

    public override string ToString() => value;

    public static Fin<CosmosId> From(string value) =>
        string.IsNullOrWhiteSpace(value) ? Error.New("Cosmos ID cannot be null or whitespace.") : new CosmosId(value);

    public static CosmosId FromOrThrow(string value) => From(value).ThrowIfFail();

    public static CosmosId Generate() => new(Guid.CreateVersion7().ToString());
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
    protected CosmosError(string Message, int Code)
        : base(Message, Code, default) { }

    public sealed record AlreadyExists : CosmosError
    {
        private AlreadyExists()
            : base("Resource already exists.", 409) { }

        public static AlreadyExists Instance { get; } = new();
    }

    public sealed record NotFound : CosmosError
    {
        private NotFound()
            : base("Resource not found.", 404) { }

        public static NotFound Instance { get; } = new();
    }

    public sealed record ETagMismatch : CosmosError
    {
        private ETagMismatch()
            : base("ETag mismatch.", 412) { }

        public static ETagMismatch Instance { get; } = new();
    }
}

public static class CosmosModule
{
    public static JsonResult<CosmosId> GetCosmosId(JsonObject jsonObject) =>
        from id in jsonObject.GetStringProperty("id")
        from cosmosId in JsonResult.Lift(CosmosId.From(id))
        select cosmosId;

    public static JsonResult<ETag> GetETag(JsonObject jsonObject) =>
        from eTag in jsonObject.GetStringProperty("_etag")
        select new ETag(eTag);

    public static Eff<ImmutableArray<JsonObject>> GetQueryResults(
        Container container,
        CosmosQueryOptions cosmosQueryOptions
    ) =>
        from iterator in GetFeedIterator(container, cosmosQueryOptions)
        from results in GetQueryResults(iterator)
        select results;

    private static Eff<FeedIterator> GetFeedIterator(Container container, CosmosQueryOptions cosmosQueryOptions) =>
        liftEff(() =>
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
                : SuccessEff(jsonObjects)
            select results;

        return getResults([]);
    }

    private static Eff<(
        ImmutableArray<JsonObject> Documents,
        Option<ContinuationToken> ContinuationToken
    )> GetCurrentPageResults(FeedIterator iterator) =>
        from cancellationToken in cancelTokenEff
        from response in use(liftEff(async () => await iterator.ReadNextAsync(cancellationToken)))
        let _ = response.EnsureSuccessStatusCode()
        let continuationToken = ContinuationToken.From(response.ContinuationToken).ToOption()
        from documents in GetDocuments(response)
        select (documents, continuationToken);

    private static Eff<ImmutableArray<JsonObject>> GetDocuments(ResponseMessage response) =>
        from jsonObject in GetJsonObjectContent(response)
        from documentsJsonArray in jsonObject.GetJsonArrayProperty("Documents").ToEff()
        from documents in documentsJsonArray.GetJsonObjects().ToEff()
        select documents;

    private static Eff<JsonObject> GetJsonObjectContent(ResponseMessage response) =>
        from node in GetJsonContent(response)
        from jsonObject in node.AsJsonObject().ToEff()
        select jsonObject;

    private static Eff<JsonNode> GetJsonContent(ResponseMessage response) =>
        from result in JsonNodeModule.Deserialize<JsonNode>(response.Content, JsonSerializerOptions.Web)
        from node in result.ToEff()
        select node;

    public static Eff<Either<CosmosError, Unit>> CreateRecord(
        Container container,
        JsonObject jsonObject,
        PartitionKey partitionKey
    ) =>
        from cancellationToken in cancelTokenEff
        let options = new ItemRequestOptions { IfNoneMatchEtag = "*" }
        from result in liftEff(async () =>
        {
            using var stream = JsonNodeModule.ToStream(jsonObject);
            using var response = await container.CreateItemStreamAsync(
                stream,
                partitionKey,
                options,
                cancellationToken
            );

            switch (response.StatusCode)
            {
                case HttpStatusCode.Conflict
                or HttpStatusCode.PreconditionFailed:
                    return Either<CosmosError, Unit>.Left(CosmosError.AlreadyExists.Instance);
                default:
                    response.EnsureSuccessStatusCode();
                    return Unit.Default;
            }
        })
        select result;

    public static Eff<Either<CosmosError, Unit>> PatchRecord(
        Container container,
        CosmosId id,
        PartitionKey partitionKey,
        IEnumerable<PatchOperation> patchOperations,
        ETag eTag
    ) =>
        from cancellationToken in cancelTokenEff
        from result in liftEff(async () =>
        {
            using var response = await container.PatchItemStreamAsync(
                id.ToString(),
                partitionKey,
                [.. patchOperations],
                new PatchItemRequestOptions { IfMatchEtag = eTag.ToString() },
                cancellationToken
            );

            switch (response.StatusCode)
            {
                case HttpStatusCode.PreconditionFailed:
                    return Either<CosmosError, Unit>.Left(CosmosError.ETagMismatch.Instance);
                case HttpStatusCode.NotFound:
                    return Either<CosmosError, Unit>.Left(CosmosError.NotFound.Instance);
                default:
                    response.EnsureSuccessStatusCode();
                    return Unit.Default;
            }
        })
        select result;

    public static Eff<Either<CosmosError, Unit>> DeleteRecord(
        Container container,
        CosmosId id,
        PartitionKey partitionKey,
        ETag eTag
    ) =>
        from cancellationToken in cancelTokenEff
        from result in liftEff(async () =>
        {
            using var response = await container.DeleteItemStreamAsync(
                id.ToString(),
                partitionKey,
                new ItemRequestOptions { IfMatchEtag = eTag.ToString() },
                cancellationToken
            );

            switch (response.StatusCode)
            {
                case HttpStatusCode.PreconditionFailed:
                    return Either<CosmosError, Unit>.Left(CosmosError.ETagMismatch.Instance);
                default:
                    response.EnsureSuccessStatusCode();
                    return Unit.Default;
            }
        })
        select result;

    public static OpenTelemetryBuilder ConfigureTelemetry(OpenTelemetryBuilder builder)
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        return builder.WithTracing(tracing => tracing.AddSource("Azure.Cosmos.Operation"));
    }

    public static PartitionKey GetPartitionKey(Order order) => GetPartitionKey(order.Id);

    public static PartitionKey GetPartitionKey(OrderId orderId) => new(orderId.ToString());
}
