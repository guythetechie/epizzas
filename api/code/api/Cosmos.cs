using Aspire.Microsoft.Azure.Cosmos;
using Azure.Core;
using common;
using LanguageExt;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace api;

internal delegate ValueTask<Either<CosmosError, Unit>> CancelCosmosOrder(OrderId orderId, ETag eTag, CancellationToken cancellationToken);
internal delegate ValueTask<Either<CosmosError, Unit>> CreateCosmosOrder(Order order, CancellationToken cancellationToken);
internal delegate ValueTask<Option<(Order, ETag)>> FindCosmosOrder(OrderId orderId, CancellationToken cancellationToken);
internal delegate ValueTask<ImmutableArray<(Order, ETag)>> ListCosmosOrders(CancellationToken cancellationToken);

internal static class CosmosModule
{
    public static void ConfigureCancelCosmosOrder(IHostApplicationBuilder builder)
    {
        CommonModule.ConfigureGetCurrentTime(builder);
        ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetCancelCosmosOrder);
    }

    private static CancelCosmosOrder GetCancelCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(OrdersContainerIdentifier);
        var getCurrentTime = provider.GetRequiredService<GetCurrentTime>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (orderId, eTag, cancellationToken) =>
        {
            using var activity = activitySource.StartActivity(nameof(CancelCosmosOrder))
                                              ?.AddTag(nameof(orderId), orderId)
                                              ?.AddTag(nameof(eTag), eTag);

            var option = await findOrder(orderId, cancellationToken);

            var resultOption = await option.MapTask(cosmosId => cancelOrder(cosmosId,
                                                                            orderId,
                                                                            eTag,
                                                                            cancellationToken));
            return resultOption.IfNone(Unit.Default);
        };

        async ValueTask<Option<CosmosId>> findOrder(OrderId orderId, CancellationToken cancellationToken)
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

            return await common.CosmosModule.GetQueryResults(container, query, cancellationToken)
                                            .Select(json => common.CosmosModule.GetCosmosId(json)
                                                                               .ThrowIfFail())
                                            .FirstOrNone(cancellationToken);
        }

        async ValueTask<Either<CosmosError, Unit>> cancelOrder(CosmosId cosmosId,
                                                               OrderId orderId,
                                                               ETag eTag,
                                                               CancellationToken cancellationToken)
        {
            var partitionKey = common.CosmosModule.GetPartitionKey(orderId);

            var status = new OrderStatus.Cancelled
            {
                By = "system",
                Date = getCurrentTime()
            };

            var statusJson = OrderStatus.Serialize(status);

            return await common.CosmosModule.PatchRecord(container,
                                                         cosmosId,
                                                         partitionKey,
                                                         [PatchOperation.Set("/status", statusJson)],
                                                         eTag,
                                                         cancellationToken);
        }
    }

    public static void ConfigureCreateCosmosOrder(IHostApplicationBuilder builder)
    {
        ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetCreateCosmosOrder);
    }

    private static CreateCosmosOrder GetCreateCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (order, cancellationToken) =>
        {
            using var activity = activitySource.StartActivity(nameof(CreateCosmosOrder))
                                              ?.AddTag(nameof(order), Order.Serialize(order));

            var orderJson = Order.Serialize(order);
            orderJson = orderJson.SetProperty("id", order.Id.ToString());
            var partitionKey = common.CosmosModule.GetPartitionKey(order);
            var result = await common.CosmosModule.CreateRecord(container, orderJson, partitionKey, cancellationToken);

            activity?.AddTag(nameof(result), result);

            return result;
        };
    }

    public static void ConfigureFindCosmosOrder(IHostApplicationBuilder builder)
    {
        ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetFindCosmosOrder);
    }

    private static FindCosmosOrder GetFindCosmosOrder(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (orderId, cancellationToken) =>
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


            var option = await common.CosmosModule.GetQueryResults(container, query, cancellationToken)
                                                  .Select(json => from order in Order.Deserialize(json)
                                                                  from eTag in common.CosmosModule.GetETag(json)
                                                                  select (order, eTag))
                                                  .Select(result => result.ThrowIfFail())
                                                  .FirstOrNone(cancellationToken);

            activity?.AddTag(nameof(option), option);

            return option;
        };
    }

    public static void ConfigureListCosmosOrders(IHostApplicationBuilder builder)
    {
        ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetListCosmosOrders);
    }

    private static ListCosmosOrders GetListCosmosOrders(IServiceProvider provider)
    {
        var container = provider.GetRequiredKeyedService<Container>(OrdersContainerIdentifier);
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async cancellationToken =>
        {
            using var activity = activitySource.StartActivity(nameof(ListCosmosOrders));

            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition(
                    """
                    SELECT c.orderId, c.status, c.pizzas, c._etag
                    FROM c
                    """)
            };


            var results = await common.CosmosModule.GetQueryResults(container, query, cancellationToken)
                                                   .Select(json => from order in Order.Deserialize(json)
                                                                   from eTag in common.CosmosModule.GetETag(json)
                                                                   select (order, eTag))
                                                   .Select(result => result.ThrowIfFail())
                                                   .ToImmutableArray(cancellationToken);

            activity?.AddTag("resultCount", results.Length);

            return results;
        };
    }

    private static string OrdersContainerIdentifier { get; } = nameof(OrdersContainerIdentifier);

    private static void ConfigureOrdersContainer(IHostApplicationBuilder builder)
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