using EPizzas.Common;
using EPizzas.Ordering.Api.V1;
using FsCheck;
using FsCheck.Fluent;

namespace EPizzas.Ordering.Api.Tests.V1;

public static class ModelGenerator
{
    public static Gen<ToppingType> ToppingType { get; } =
        Gen.Elements(new ToppingType[]
        {
            new ToppingType.Pepperoni(),
            new ToppingType.Ham(),
            new ToppingType.Cheese()
        });

    public static Gen<ToppingAmount> ToppingAmount { get; } =
        Gen.Elements(new ToppingAmount[]
        {
            new ToppingAmount.Light(),
            new ToppingAmount.Medium(),
            new ToppingAmount.Extra()
        });

    public static Gen<Topping> Topping { get; } =
        from type in ToppingType
        from amount in ToppingAmount
        select new Topping
        {
            Amount = amount,
            Type = type
        };

    public static Gen<PizzaSize> PizzaSize { get; } =
        Gen.Elements(new PizzaSize[]
        {
            new PizzaSize.Small(),
            new PizzaSize.Medium(),
            new PizzaSize.Large()
        });

    public static Gen<Pizza> Pizza { get; } =
        from size in PizzaSize
        from toppings in Topping.ListOf()
        select new Pizza
        {
            Size = size,
            Toppings = toppings
        };

    public static Gen<OrderId> OrderId { get; } =
        from id in Generator.AlphaNumericString.Where(x => string.IsNullOrWhiteSpace(x) is false)
        select new OrderId(id);

    public static Gen<OrderStatus> OrderStatus { get; } =
        Gen.Elements(new OrderStatus[]
        {
            new OrderStatus.New(),
            new OrderStatus.Canceled()
        });

    public static Gen<Order> Order { get; } =
        from id in OrderId
        from pizzas in Pizza.NonEmptyListOf()
        from status in OrderStatus
        select new Order
        {
            Id = id,
            Pizzas = pizzas,
            Status = status
        };
}
