using Asp.Versioning.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace EPizzas.Ordering.Api.V1;

internal static class Endpoints
{
    public static void Map(IVersionedEndpointRouteBuilder builder)
    {
        Orders.Endpoints.Map(builder);
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {
        Orders.Services.Configure(services);
    }
}