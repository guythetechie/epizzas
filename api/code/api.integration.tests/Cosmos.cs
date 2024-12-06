using Aspire.Microsoft.Azure.Cosmos;
using Azure.Core;
using common;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using static LanguageExt.Prelude;

namespace api.integration.tests;

internal sealed record OrdersContainer(Container Value);

internal static class CosmosModule
{
    public static Eff<OrdersContainer, Unit> EmptyOrdersContainer() =>
        from orders in ListOrders()
        from result in orders.AsIterable().Traverse(DeleteOrder)
        select Unit.Default;

    private static Eff<OrdersContainer, ImmutableArray<JsonObject>> ListOrders() =>
        from container in runtime<OrdersContainer>()
        let query = new CosmosQueryOptions
        {
            Query = new QueryDefinition("SELECT c.id, c.orderId, c.etag FROM c")
        }
        from orders in common.CosmosModule.GetQueryResults(container.Value, query)
        select orders;

    private static Eff<OrdersContainer, Unit> DeleteOrder(JsonObject orderJson)
    {
        var jsonResult =
            from cosmosId in common.CosmosModule.GetCosmosId(orderJson)
            from orderIdString in orderJson.GetStringProperty("orderId")
            from eTag in common.CosmosModule.GetETag(orderJson)
            select (cosmosId, orderIdString, eTag);

        return
            from x in jsonResult.ToEff().WithRuntime<OrdersContainer>()
            from orderId in OrderId.From(x.orderIdString).ToEff()
            let partitionKey = common.CosmosModule.GetPartitionKey(orderId)
            from container in runtime<OrdersContainer>()
            from result in common.CosmosModule.DeleteRecord(container.Value, x.cosmosId, partitionKey, x.eTag)
            from _ in result.ToEff(error => Error.New($"Failed to delete order. Error is '{error}'."))
            select Unit.Default;
    }

    public static void ConfigureOrdersContainer(IHostApplicationBuilder builder)
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