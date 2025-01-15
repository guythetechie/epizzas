using Aspire.Microsoft.Azure.Cosmos;
using Azure.Core;
using common;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using static LanguageExt.Prelude;

namespace api;

internal delegate DateTimeOffset GetCurrentTime();
internal delegate Eff<Either<CosmosError, Unit>> CancelCosmosOrder(OrderId orderId, ETag eTag);
internal delegate Eff<Either<CosmosError, Unit>> CreateCosmosOrder(Order order);
internal delegate Eff<Option<(Order, ETag)>> FindCosmosOrder(OrderId orderId);
internal delegate Eff<ImmutableArray<(Order, ETag)>> ListCosmosOrders();

internal static class OrdersModule
{
    public static void ConfigureBuilder(IHostApplicationBuilder builder) =>
        Module.Configure(builder);

    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/v1/orders");

        group.MapDelete("/{orderId}", Handlers.Cancel);
        group.MapPost("/", Handlers.Create);
        group.MapGet("/{orderId}", Handlers.GetById);
        group.MapGet("/", Handlers.List);
    }
}

file static class Handlers
{
    public static async ValueTask<IResult> Cancel(
        string orderId,
        [FromServices] CancelCosmosOrder cancelCosmosOrder,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken
    )
    {
        var operation =
            from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
            from eTag in ApiOperation.Lift(getETag(ifMatch))
            from _ in ApiOperation.Lift(cancelOrder(validatedOrderId, eTag, cancelCosmosOrder))
            select getSuccessfulResponse();

        return await operation.Run(cancellationToken);

        static Either<IResult, OrderId> validateOrderId(string orderId) =>
            OrderId.From(orderId)
                   .ToEither(error => Results.BadRequest(new
                   {
                       code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                       message = error.Message
                   }));

        static Either<IResult, ETag> getETag(string? ifMatch) =>
            ETag.From(ifMatch)
                .ToEither(error => Results.BadRequest(new
                {
                    code = ApiErrorCode.InvalidRequestHeader.Instance.ToString(),
                    message = "If-Match header is required.",
                }));

        static Eff<Either<IResult, Unit>> cancelOrder(
            OrderId orderId,
            ETag eTag,
            CancelCosmosOrder cancelCosmosOrder
        ) =>
            from result in cancelCosmosOrder(orderId, eTag)
            select result.Match(error => error switch
            {
                CosmosError.NotFound => Either<IResult, Unit>.Right(Unit.Default),
                CosmosError.ETagMismatch => Either<IResult, Unit>.Left(Results.Json(new
                {
                    code = ApiErrorCode.ETagMismatch.Instance.ToString(),
                    message = $"Could not cancel order '{orderId}'. Another process might have modified the resource. Please try again.",
                },
                    statusCode: (int)HttpStatusCode.PreconditionFailed
                )),
                _ => throw error.ToException()
            }, _ => Unit.Default);

        static IResult getSuccessfulResponse() => Results.NoContent();
    }

    public static async ValueTask<IResult> Create(
        [FromServices] CreateCosmosOrder createCosmosOrder,
        [FromServices] GetCurrentTime getCurrentTime,
        Stream? body,
        CancellationToken cancellationToken
    )
    {
        var operation =
            from order in ApiOperation.Lift(parseOrder(body, getCurrentTime))
            from _ in ApiOperation.Lift(createOrder(createCosmosOrder, order))
            select getSuccessfulResponse();

        return await operation.Run(cancellationToken);

        static Eff<Either<IResult, Order>> parseOrder(Stream? body, GetCurrentTime getCurrentTime) =>
            from result in JsonNodeModule.FromStream(body)
            let orderResult = from jsonNode in result
                              from jsonObject in jsonNode.AsJsonObject()
                              let status = new OrderStatus.Created
                              {
                                  Date = getCurrentTime(),
                                  By = "system"
                              }
                              let statusJson = OrderStatus.Serialize(status)
                              let orderJson = jsonObject.SetProperty("status", statusJson)
                              from order in Order.Deserialize(orderJson)
                              select order
            select orderResult.ToEither(error => Results.BadRequest(new
            {
                code = ApiErrorCode.InvalidRequestBody.Instance.ToString(),
                message = error.Message,
                details = (string[])[.. error.Details.Select(error => error.Message)]
            }));

        static Eff<Either<IResult, Unit>> createOrder(CreateCosmosOrder createCosmosOrder, Order order) =>
            from either in createCosmosOrder(order)
            select either.MapLeft(error => error switch
            {
                CosmosError.AlreadyExists or CosmosError.ETagMismatch => Results.Conflict(new
                {
                    code = ApiErrorCode.ResourceAlreadyExists.Instance.ToString(),
                    message = $"Order with ID {order.Id} already exists.",
                }),
                _ => throw error.ToException()
            });

        static IResult getSuccessfulResponse() => Results.NoContent();
    }

    public static async ValueTask<IResult> GetById(
        string orderId,
        [FromServices] FindCosmosOrder findCosmosOrder,
        CancellationToken cancellationToken
    )
    {
        var operation =
            from validatedOrderId in ApiOperation.Lift(validateOrderId(orderId))
            from orderAndETag in ApiOperation.Lift(getOrder(validatedOrderId, findCosmosOrder))
            let successfulResponse = getSuccessfulResponse(orderAndETag.Item1, orderAndETag.Item2)
            select successfulResponse;

        return await operation.Run(cancellationToken);

        static Either<IResult, OrderId> validateOrderId(string orderId) =>
            OrderId.From(orderId)
                   .ToEither(error => Results.BadRequest(new
                   {
                       code = ApiErrorCode.InvalidRequestParameter.Instance.ToString(),
                       message = error.Message
                   }));

        static Eff<Either<IResult, (Order, ETag)>> getOrder(OrderId orderId, FindCosmosOrder findCosmosOrder) =>
            from option in findCosmosOrder(orderId)
            select option.ToEither(() => Results.NotFound(new
            {
                code = ApiErrorCode.ResourceNotFound.Instance.ToString(),
                message = $"Order with ID {orderId} was not found.",
            }));

        static IResult getSuccessfulResponse(Order order, ETag eTag) =>
            Results.Ok(new JsonObject
            {
                ["eTag"] = eTag.ToString(),
                ["status"] = OrderStatus.Serialize(order.Status),
                ["pizzas"] = order.Pizzas.Select(Pizza.Serialize).ToJsonArray(),
            });
    }

    public static async ValueTask<IResult> List(
        [FromServices] ListCosmosOrders listCosmosOrders,
        CancellationToken cancellationToken
    )
    {
        var operation = from orders in ApiOperation.Lift(listCosmosOrders())
                        select getSuccessfulResponse(orders);

        return await operation.Run(cancellationToken);

        static IResult getSuccessfulResponse(IEnumerable<(Order, ETag)> orders) =>
            Results.Ok(new JsonObject
            {
                ["values"] = orders.Select(order => Order.Serialize(order.Item1)
                                                         .SetProperty("eTag", order.Item2.ToString()))
                                   .ToJsonArray(),
            });
    }
}

file static class Module
{
    public static void Configure(IHostApplicationBuilder builder)
    {
        ConfigureGetCurrentTime(builder);
        ConfigureCancelCosmosOrder(builder);
        ConfigureCreateCosmosOrder(builder);
        ConfigureFindCosmosOrder(builder);
        ConfigureListCosmosOrders(builder);
    }

    private static void ConfigureGetCurrentTime(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(GetGetCurrentTime);
    }

    private static GetCurrentTime GetGetCurrentTime(IServiceProvider provider)
    {
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return () =>
        {
            var activity = activitySource.StartActivity(nameof(GetCurrentTime));

            var time = DateTimeOffset.UtcNow;

            activity?.AddTag(nameof(time), time);

            return time;
        };
    }

    private static void ConfigureCancelCosmosOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);
        ConfigureGetCurrentTime(builder);

        builder.Services.TryAddSingleton(GetCancelCosmosOrder);
    }

    private static CancelCosmosOrder GetCancelCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var getCurrentTime = provider.GetRequiredService<GetCurrentTime>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return (orderId, eTag) =>
            liftIO(async env =>
            {
                using var activity = activitySource.StartActivity(nameof(CancelCosmosOrder))
                                                  ?.AddTag(nameof(orderId), orderId)
                                                  ?.AddTag(nameof(eTag), eTag);

                var resultEff = from option in findOrder(orderId)
                                from result in option.Traverse(cosmosId => cancelOrder(cosmosId, orderId, eTag))
                                select result.IfNone(() => Unit.Default);

                var either = await resultEff.RunUnsafe(env.Token);

                activity?.AddTag(nameof(either), either);

                return either;
            });

        Eff<Option<CosmosId>> findOrder(OrderId orderId)
        {
            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition(
                    """
                    SELECT c.id
                    FROM c
                    WHERE c.orderId = @orderId
                    """
                ).WithParameter("@orderId", orderId.ToString()),
            };

            return from queryResults in common.CosmosModule.GetQueryResults(container, query)
                   let firstResultOption = queryResults.HeadOrNone()
                   from cosmosId in firstResultOption.Traverse(json => common.CosmosModule.GetCosmosId(json).ToEff())
                   select cosmosId;
        }

        Eff<Either<CosmosError, Unit>> cancelOrder(CosmosId cosmosId, OrderId orderId, ETag eTag)
        {
            var partitionKey = common.CosmosModule.GetPartitionKey(orderId);
            var status = new OrderStatus.Cancelled
            {
                By = "system",
                Date = getCurrentTime()
            };
            var statusJson = OrderStatus.Serialize(status);
            var jObject = Newtonsoft.Json.Linq.JObject.Parse(statusJson.ToJsonString());

            return common.CosmosModule.PatchRecord(
                container,
                cosmosId,
                partitionKey,
                [PatchOperation.Set("/status", jObject)],
                eTag
            );
        }
    }

    private static void ConfigureCreateCosmosOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetCreateCosmosOrder);
    }

    private static CreateCosmosOrder GetCreateCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return order =>
            liftIO(async env =>
            {
                using var activity = activitySource.StartActivity(nameof(CreateCosmosOrder))
                                                  ?.AddTag(nameof(order), Order.Serialize(order));

                var orderJson = Order.Serialize(order);
                var cosmosId = CosmosId.Generate();
                orderJson = orderJson.SetProperty("id", cosmosId.ToString());
                var partitionKey = common.CosmosModule.GetPartitionKey(order);
                var result = await common.CosmosModule.CreateRecord(container, orderJson, partitionKey).RunUnsafe(env.Token);

                activity?.AddTag(nameof(result), result);

                return result;
            });
    }

    private static void ConfigureFindCosmosOrder(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetFindCosmosOrder);
    }

    private static FindCosmosOrder GetFindCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return orderId =>
            liftIO(async env =>
            {
                using var activity = activitySource.StartActivity(nameof(FindCosmosOrder))
                                                  ?.AddTag(nameof(orderId), orderId);

                var query = new CosmosQueryOptions
                {
                    Query = new QueryDefinition(
                        """
                        SELECT c.orderId, c.status, c.pizzas, c._etag
                        FROM c
                        WHERE c.orderId = @orderId
                        """
                    ).WithParameter("@orderId", orderId.ToString()),
                };

                var resultEff = from queryResults in common.CosmosModule.GetQueryResults(container, query)
                                let option = from json in queryResults.HeadOrNone()
                                             let eTag = common.CosmosModule.GetETag(json).ThrowIfFail()
                                             let order = Order.Deserialize(json).ThrowIfFail()
                                             select (order, eTag)
                                select option;

                var result = await resultEff.RunUnsafe(env.Token);

                activity?.AddTag(nameof(result), result);

                return result;
            });
    }

    private static void ConfigureListCosmosOrders(IHostApplicationBuilder builder)
    {
        CosmosModule.ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetListCosmosOrders);
    }

    private static ListCosmosOrders GetListCosmosOrders(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(CosmosModule.OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return () =>
            liftIO(async env =>
            {
                using var activity = activitySource.StartActivity(nameof(ListCosmosOrders));

                var query = new CosmosQueryOptions
                {
                    Query = new QueryDefinition("SELECT c.orderId, c.status, c.pizzas, c._etag FROM c"),
                };

                var resultEff = from queryResults in common.CosmosModule.GetQueryResults(container, query)
                                from orders in queryResults
                                    .AsIterable()
                                    .Traverse(jsonObject =>
                                        from eTag in common.CosmosModule.GetETag(jsonObject)
                                        from order in Order.Deserialize(jsonObject)
                                        select (order, eTag)
                                    )
                                    .ToEff()
                                select orders.ToImmutableArray();

                var result = await resultEff.RunUnsafe(env.Token);

                activity?.AddSerializedTag(nameof(result), result);

                return result;
            }); ;
    }
}

file static class CosmosModule
{
    public static string OrdersContainerIdentifier { get; } = nameof(OrdersContainerIdentifier);

    public static void ConfigureOrdersContainer(IHostApplicationBuilder builder)
    {
        ConfigureDatabase(builder);

        builder.Services.TryAddKeyedSingleton(OrdersContainerIdentifier, (provider, _) => GetOrdersContainer(provider));
    }

    private static Container GetOrdersContainer(IServiceProvider provider)
    {
        var database = provider.GetRequiredService<Database>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var containerName = configuration.GetValueOrThrow("COSMOS_ORDERS_CONTAINER_NAME");

        return database.GetContainer(containerName);
    }

    private static void ConfigureDatabase(IHostApplicationBuilder builder)
    {
        ConfigureCosmosClient(builder);

        builder.Services.TryAddSingleton(GetDatabase);
    }

    private static Database GetDatabase(IServiceProvider provider)
    {
        var client = provider.GetRequiredService<CosmosClient>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var databaseName = configuration.GetValueOrThrow("COSMOS_DATABASE_NAME");

        return client.GetDatabase(databaseName);
    }

    private static void ConfigureCosmosClient(IHostApplicationBuilder builder)
    {
        AzureModule.ConfigureTokenCredential(builder);

        var configuration = builder.Configuration;
        var connectionName = configuration.GetValue("COSMOS_CONNECTION_NAME").IfNone(string.Empty);
        var provider = builder.Services.BuildServiceProvider();

        builder.AddAzureCosmosClient(connectionName, configureSettings, configureClientOptions);

        void configureSettings(MicrosoftAzureCosmosSettings settings)
        {
            configuration
                .GetValue("COSMOS_ACCOUNT_ENDPOINT")
                .Map(endpoint => new Uri(endpoint, UriKind.Absolute))
                .Iter(endpoint =>
                {
                    settings.AccountEndpoint = endpoint;
                    settings.Credential = provider.GetRequiredService<TokenCredential>();
                });

            configuration
                .GetValue("COSMOS_CONNECTION_STRING")
                .Iter(connectionString => settings.ConnectionString = connectionString);
        }

        void configureClientOptions(CosmosClientOptions options)
        {
            options.EnableContentResponseOnWrite = false;
            options.UseSystemTextJsonSerializerWithOptions = JsonSerializerOptions.Web;
            options.CosmosClientTelemetryOptions = new() { DisableDistributedTracing = false };
        }
    }
}
