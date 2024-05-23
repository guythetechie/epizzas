using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace api.v1;

internal static class Endpoints
{
    public static void Map(IVersionedEndpointRouteBuilder builder)
    {
        builder.HasApiVersion(1, 0);

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