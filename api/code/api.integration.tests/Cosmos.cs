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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace api.integration.tests;

internal delegate ValueTask EmptyOrdersContainer(CancellationToken cancellationToken);

internal sealed record OrdersContainer(Container Value);

internal static class CosmosModule
{
    public static void ConfigureEmptyOrdersContainer(IHostApplicationBuilder builder)
    {
        ConfigureOrdersContainer(builder);

        builder.Services.TryAddSingleton(GetEmptyOrdersContainer);
    }

    private static EmptyOrdersContainer GetEmptyOrdersContainer(IServiceProvider provider)
    {
        var ordersContainer = provider.GetRequiredService<OrdersContainer>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        var container = ordersContainer.Value;

        return async cancellationToken =>
        {
            using var activity = activitySource.StartActivity(nameof(EmptyOrdersContainer));

            var deletedCount = 0;

            await listOrders(cancellationToken)
                    .IterParallel(async x =>
                    {
                        var (cosmosId, partitionKey, eTag) = x;

                        await common.CosmosModule.DeleteRecord(container,
                                                               cosmosId,
                                                               partitionKey,
                                                               eTag,
                                                               cancellationToken);

                        Interlocked.Increment(ref deletedCount);
                    }, cancellationToken);

            activity?.AddTag(nameof(deletedCount), deletedCount);
        };

        IAsyncEnumerable<(CosmosId, PartitionKey, ETag)> listOrders(CancellationToken cancellationToken)
        {
            var query = new CosmosQueryOptions
            {
                Query = new QueryDefinition("""
                    SELECT c.id, c.orderId, c._etag
                    FROM c
                    """)
            };

            return common.CosmosModule.GetQueryResults(container, query, cancellationToken)
                         .Select(json => from id in common.CosmosModule.GetCosmosId(json)
                                         from partitionKeyString in json.GetStringProperty("orderId")
                                         let partitionKey = new PartitionKey(partitionKeyString)
                                         from eTag in common.CosmosModule.GetETag(json)
                                         select (id, partitionKey, eTag))
                         .Select(result => result.ThrowIfFail());
        }
    }

    private static void ConfigureOrdersContainer(IHostApplicationBuilder builder)
    {
        ConfigureDatabase(builder);

        builder.Services.TryAddSingleton(GetOrdersContainer);
    }

    private static OrdersContainer GetOrdersContainer(IServiceProvider provider)
    {
        var database = provider.GetRequiredService<Database>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var containerName = configuration.GetValueOrThrow("COSMOS_ORDERS_CONTAINER_NAME");

        return new(database.GetContainer(containerName));
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