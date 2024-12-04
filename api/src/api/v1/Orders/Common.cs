using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace api.v1.Orders;

internal static class CommonModule
{
    public static void ConfigureEndpoints(IEndpointRouteBuilder builder)
    {
        var ordersBuilder = builder.MapGroup("/orders");

        GetByIdModule.ConfigureEndpoints(ordersBuilder);
        ListModule.ConfigureEndpoints(ordersBuilder);
        CreateModule.ConfigureEndpoints(ordersBuilder);
        CancelModule.ConfigureEndpoints(ordersBuilder);
    }

    public static void ConfigureApplicationBuilder(IHostApplicationBuilder builder)
    {
        GetByIdModule.ConfigureApplicationBuilder(builder);
        ListModule.ConfigureApplicationBuilder(builder);
        CreateModule.ConfigureApplicationBuilder(builder);
        CancelModule.ConfigureApplicationBuilder(builder);
    }
}