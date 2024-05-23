using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace api.v1.Orders;

internal static class Endpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        var ordersBuilder = builder.MapGroup("/orders");

        GetByIdEndpoints.Map(ordersBuilder);
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {
        GetByIdServices.Configure(services);
    }
}