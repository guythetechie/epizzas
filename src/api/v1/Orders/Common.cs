using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace api.v1.Orders;

internal static class Endpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        var ordersBuilder = builder.MapGroup("/orders");

        GetByIdEndpoints.Map(ordersBuilder);
        ListEndpoints.Map(ordersBuilder);
        CreateEndpoints.Map(ordersBuilder);
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {
        GetByIdServices.Configure(services);
        ListServices.Configure(services);
        CreateServices.Configure(services);
    }

    public static void ConfigureOrdersCosmosContainer(IServiceCollection services)
    {
        services.TryAddSingleton(GetOrdersCosmosContainer);
    }

    private static OrdersCosmosContainer GetOrdersCosmosContainer(IServiceProvider provider)
    {
        var database = provider.GetRequiredService<Database>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var containerName = configuration.GetValue("COSMOS_ORDERS_CONTAINER_NAME");
        var container = database.GetContainer(containerName);
        return new OrdersCosmosContainer(container);
    }
}

internal sealed record OrdersCosmosContainer(Container Value);