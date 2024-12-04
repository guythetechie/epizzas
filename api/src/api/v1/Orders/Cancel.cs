using Azure;
using common;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace api.v1.Orders;

internal delegate Eff<Either<CosmosError.ETagMismatch, Unit>> CancelCosmosOrder(OrderId orderId, ETag eTag);

internal static class CancelModule
{
    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/{orderId}", handle);

        static async ValueTask<IResult> handle(string orderId, [FromServices] CancelCosmosOrder cancelCosmosOrder, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
        {
            var operation = from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
                            from eTag in ApiOperation.Lift(getETag(ifMatch))
                            from _ in ApiOperation.Lift(cancelOrder(validatedOrderId, eTag, cancelCosmosOrder))
                            select getSuccessfulResponse();

            return await operation.Run(cancellationToken);
        };

        static Either<IResult, OrderId> validateOrderId(string orderId) =>
            OrderId.From(orderId)
                   .ToEither()
                   .MapLeft(error => Results.BadRequest(new
                   {
                       code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                       message = error.Message
                   }));

        static Either<IResult, ETag> getETag(string? ifMatch) =>
            string.IsNullOrWhiteSpace(ifMatch)
            ? Either<IResult, ETag>.Left(Results.BadRequest(new
            {
                code = ApiErrorCode.InvalidRequestHeader.Instance.ToString(),
                message = "If-Match header is required."
            }))
            : new ETag(ifMatch);

        static Eff<Either<IResult, Unit>> cancelOrder(OrderId orderId, ETag eTag, CancelCosmosOrder cancelCosmosOrder) =>
            from result in cancelCosmosOrder(orderId, eTag)
            select result.MapLeft(error => Results.Json(new
            {
                code = ApiErrorCode.ETagMismatch.Instance.ToString(),
                message = $"Could not cancel order '{orderId}'. Another process might have modified the resource. Please try again."
            }, statusCode: (int)HttpStatusCode.PreconditionFailed));

        static IResult getSuccessfulResponse() =>
            Results.NoContent();
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder)
    {
        ConfigureCancelCosmosOrder(builder);
    }

    private static void ConfigureCancelCosmosOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);
        api.CommonModule.ConfigureTimeProvider(builder);

        builder.Services.TryAddSingleton(GetCancelCosmosOrder);
    }

    private static CancelCosmosOrder GetCancelCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return (orderId, eTag) =>
        {
            using var _ = activitySource.StartActivity("cosmos.cancel_order")
                                       ?.AddTag("order_id", orderId);

            return from option in findOrder(orderId)
                   from result in option.Traverse(cosmosId => cancelOrder(cosmosId, orderId, eTag))
                   select result.IfNone(() => Unit.Default);
        };

        Eff<Option<CosmosId>> findOrder(OrderId orderId)
        {
            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition("""
                                            SELECT c.id
                                            FROM c
                                            WHERE c.orderId = @orderId
                                            """
                                            ).WithParameter("@orderId", orderId.ToString())
            };

            return from queryResults in common.CosmosModule.GetQueryResults(container, query)
                   let firstResultOption = queryResults.HeadOrNone()
                   from cosmosId in firstResultOption.Traverse(json => common.CosmosModule.GetCosmosId(json).ToEff())
                   select cosmosId;
        }

        Eff<Either<CosmosError.ETagMismatch, Unit>> cancelOrder(CosmosId cosmosId, OrderId orderId, ETag eTag)
        {
            var partitionKey = CosmosModule.GetPartitionKey(orderId);
            var status = new OrderStatus.Cancelled
            {
                By = "system",
                Date = timeProvider.GetUtcNow()
            };
            var statusJson = OrderStatus.Serialize(status);
            var jObject = Newtonsoft.Json.Linq.JObject.Parse(statusJson.ToJsonString());

            return common.CosmosModule.PatchRecord(container,
                                                   cosmosId,
                                                   partitionKey,
                                                   [PatchOperation.Set("/status", jObject)],
                                                   eTag);
        }
    }
}