using Asp.Versioning.Builder;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace EPizzas.Ordering.Api.V1.Orders;

internal static class Endpoints
{
    public static void Map(IVersionedEndpointRouteBuilder builder)
    {
        var groupBuilder = builder.MapGroup("/v{version:apiVersion}/orders")
                                  .HasApiVersion(1);

        Get.Endpoints.Map(groupBuilder);
        Cancel.Endpoints.Map(groupBuilder);
        List.Endpoints.Map(groupBuilder);
        Put.Endpoints.Map(groupBuilder);
    }
}

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {
        Get.Services.Configure(services);
        Cancel.Services.Configure(services);
        List.Services.Configure(services);
        Put.Services.Configure(services);
    }
}

internal static class Common
{
    public static Either<string, OrderId> TryGetOrderId(string orderId)
    {
        return string.IsNullOrWhiteSpace(orderId)
                ? "Order ID cannot be null or whitespace."
                : new OrderId(orderId);
    }
}