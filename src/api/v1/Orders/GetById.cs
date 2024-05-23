using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace api.v1.Orders;

internal static class GetByIdEndpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{orderId}", Handle);
    }

    private static Order Handle(string orderId) =>
        new()
        {
            Id = new OrderId(orderId),
            Pizzas = [new Pizza
                {
                    Toppings = ImmutableDictionary.Create<PizzaTopping, PizzaToppingAmount>()
                                                  .Add(PizzaTopping.Cheese.Instance, PizzaToppingAmount.Light.Instance)
                                                  .ToFrozenDictionary(),
                    Size = PizzaSize.Medium.Instance
                }]
        };

    internal static class GetByIdServices
    {
        public static void Configure(IServiceCollection services)
        {
        }
    }
}

internal static class GetByIdServices
{
    public static void Configure(IServiceCollection services)
    {
    }
}