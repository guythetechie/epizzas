using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace api.v1;

internal static class CommonModule
{
    public static void ConfigureEndpoints(IVersionedEndpointRouteBuilder builder)
    {
        builder.HasApiVersion(1, 0);

        Orders.CommonModule.ConfigureEndpoints(builder);
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder) =>
        Orders.CommonModule.ConfigureApplicationBuilder(builder);
}