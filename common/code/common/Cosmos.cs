using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Azure.Cosmos;
using OpenTelemetry;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

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
        from eTagString in jsonObject.GetStringProperty("_etag")
        from eTag in JsonResult.Lift(ETag.From(eTagString))
        select eTag;

    public static async IAsyncEnumerable<JsonObject> GetQueryResults(Container container,
                                                                     CosmosQueryOptions cosmosQueryOptions,
                                                                     [EnumeratorCancellation]
                                                                     CancellationToken cancellationToken)
    {
        using var iterator = GetFeedIterator(container, cosmosQueryOptions);

        await foreach (var result in GetQueryResults(iterator, cancellationToken))
        {
            yield return result;
        }
    }

    private static FeedIterator GetFeedIterator(Container container, CosmosQueryOptions cosmosQueryOptions)
    {
        var queryDefinition = cosmosQueryOptions.Query;
        var continuationToken = cosmosQueryOptions.ContinuationToken.ValueUnsafe()?.ToString();

        var queryRequestOptions = new QueryRequestOptions();
        cosmosQueryOptions.PartitionKey.Iter(partitionKey => queryRequestOptions.PartitionKey = partitionKey);

        return container.GetItemQueryStreamIterator(queryDefinition, continuationToken, queryRequestOptions);
    }

    private static async IAsyncEnumerable<JsonObject> GetQueryResults(FeedIterator iterator,
                                                                      [EnumeratorCancellation]
                                                                      CancellationToken cancellationToken)
    {
        Option<ContinuationToken> continuationToken;

        do
        {
            (var documents, continuationToken) = await GetCurrentPageResults(iterator, cancellationToken);
            foreach (var document in documents)
            {
                yield return document;
            }
        }
        while (continuationToken.IsSome);
    }

    private static async ValueTask<(
        ImmutableArray<JsonObject> Documents,
        Option<ContinuationToken> ContinuationToken
    )> GetCurrentPageResults(FeedIterator iterator, CancellationToken cancellationToken)
    {
        using var response = await iterator.ReadNextAsync(cancellationToken);

        response.EnsureSuccessStatusCode();

        var documents = await GetDocuments(response, cancellationToken);

        var continuationToken = ContinuationToken.From(response.ContinuationToken)
                                                 .ToOption();

        return (documents, continuationToken);
    }

    private static async ValueTask<ImmutableArray<JsonObject>> GetDocuments(ResponseMessage response,
                                                                            CancellationToken cancellationToken)
    {
        var result = from jsonNode in await JsonNodeModule.From(response.Content, cancellationToken: cancellationToken)
                     from jsonObject in jsonNode.AsJsonObject()
                     from documentsJsonArray in jsonObject.GetJsonArrayProperty("Documents")
                     from documents in documentsJsonArray.GetJsonObjects()
                     select documents;

        return result.ThrowIfFail();
    }

    public static async ValueTask<Either<CosmosError, Unit>> CreateRecord(Container container,
                                                                          JsonObject jsonObject,
                                                                          PartitionKey partitionKey,
                                                                          CancellationToken cancellationToken)
    {
        using var stream = JsonNodeModule.ToStream(jsonObject);

        using var response =
            await container.CreateItemStreamAsync(stream,
                                                  partitionKey,
                                                  new ItemRequestOptions
                                                  {
                                                      
                                                      IfNoneMatchEtag = ETag.All.ToString()
                                                  },
                                                  cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed:
                return CosmosError.AlreadyExists.Instance;
            default:
                response.EnsureSuccessStatusCode();
                return Unit.Default;
        }
    }

    public static async ValueTask<Either<CosmosError, Unit>> PatchRecord(Container container,
                                                                         CosmosId id,
                                                                         PartitionKey partitionKey,
                                                                         IEnumerable<PatchOperation> patchOperations,
                                                                         ETag eTag,
                                                                         CancellationToken cancellationToken)
    {
        using var response =
            await container.PatchItemStreamAsync(id.ToString(),
                                                 partitionKey,
                                                 [.. patchOperations],
                                                 new PatchItemRequestOptions
                                                 {
                                                     IfMatchEtag = eTag.ToString()
                                                 },
                                                 cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.PreconditionFailed:
                return CosmosError.ETagMismatch.Instance;
            case HttpStatusCode.NotFound:
                return CosmosError.NotFound.Instance;
            default:
                response.EnsureSuccessStatusCode();
                return Unit.Default;
        }
    }

    public static async ValueTask<Either<CosmosError, Unit>> DeleteRecord(Container container,
                                                                          CosmosId id,
                                                                          PartitionKey partitionKey,
                                                                          ETag eTag,
                                                                          CancellationToken cancellationToken)
    {
        using var response =
            await container.DeleteItemStreamAsync(id.ToString(),
                                                  partitionKey,
                                                  new ItemRequestOptions
                                                  {
                                                      IfMatchEtag = eTag.ToString()
                                                  },
                                                  cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.PreconditionFailed:
                return CosmosError.ETagMismatch.Instance;
            case HttpStatusCode.NotFound:
                return Unit.Default;
            default:
                response.EnsureSuccessStatusCode();
                return Unit.Default;
        }
    }

    public static OpenTelemetryBuilder ConfigureTelemetry(OpenTelemetryBuilder builder)
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        return builder.WithTracing(tracing => tracing.AddSource("Azure.Cosmos.Operation"));
    }

    public static PartitionKey GetPartitionKey(Order order) => GetPartitionKey(order.Id);

    public static PartitionKey GetPartitionKey(OrderId orderId) => new(orderId.ToString());
}
