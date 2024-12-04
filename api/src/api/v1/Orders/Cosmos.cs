using Azure.Core;
using common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

namespace api.v1.Orders;

internal static class CosmosModule
{
    public static string OrdersContainerIdentifier { get; } = nameof(OrdersContainerIdentifier);

    public static void ConfigureOrdersContainer(IHostApplicationBuilder builder)
    {
        ConfigureDatabase(builder);

        builder.Services.TryAddKeyedSingleton(OrdersContainerIdentifier,
                                              (provider, _) => GetOrdersContainer(provider));
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
        var configuration = builder.Configuration;
        var aspireConnectionName = configuration.GetValue("ASPIRE_COSMOS_CONNECTION_NAME")
                                                .IfNone(string.Empty);

        builder.AddAzureCosmosClient(aspireConnectionName,
                                     settings =>
                                     {
                                         configuration.GetValue("COSMOS_ACCOUNT_ENDPOINT")
                                                      .Iter(endpoint =>
                                                      {
                                                          if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                                                          {
                                                              settings.AccountEndpoint = uri;

                                                              var tokenCredential = builder.Services.BuildServiceProvider().GetRequiredService<TokenCredential>();
                                                              settings.Credential = tokenCredential;
                                                          }
                                                      });

                                         configuration.GetValue("COSMOS_CONNECTION_STRING")
                                                      .Iter(connectionString => settings.ConnectionString = connectionString);
                                     });
    }

    public static PartitionKey GetPartitionKey(Order order) =>
        GetPartitionKey(order.Id);

    public static PartitionKey GetPartitionKey(OrderId orderId) =>
        new(orderId.ToString());
}