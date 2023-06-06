using CommunityToolkit.Diagnostics;
using LanguageExt;
using System.Collections.Generic;

namespace EPizzas.Ordering.Api.V1;

public abstract record ToppingType
{
    public sealed record Pepperoni : ToppingType;
    public sealed record Ham : ToppingType;
    public sealed record Cheese : ToppingType;
}

public abstract record ToppingAmount
{
    public sealed record Light : ToppingAmount;
    public sealed record Medium : ToppingAmount;
    public sealed record Extra : ToppingAmount;
}

public sealed record Topping
{
    public required ToppingType Type { get; init; }
    public required ToppingAmount Amount { get; init; }
}

public abstract record PizzaSize
{
    public sealed record Small : PizzaSize;
    public sealed record Medium : PizzaSize;
    public sealed record Large : PizzaSize;
}

public sealed record Pizza
{
    public required PizzaSize Size { get; init; }
    public required IReadOnlyList<Topping> Toppings { get; init; }
}

public sealed record OrderId
{
    public OrderId(string value)
    {
        Guard.IsNotNullOrWhiteSpace(value, nameof(value));

        Value = value;
    }

    public string Value { get; }
}

public abstract record OrderStatus
{
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public sealed record New : OrderStatus;
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    public sealed record Canceled : OrderStatus;
}

public sealed record Order
{
    private readonly Seq<Pizza> pizzas;

    public required OrderId Id { get; init; }

    public IReadOnlyList<Pizza> Pizzas
    {
        get => pizzas.Freeze();
        init => pizzas = ValidatePizzas(value).ToSeq();
    }

    public required OrderStatus Status { get; init; }

    private static IReadOnlyList<Pizza> ValidatePizzas(IReadOnlyList<Pizza> pizzas)
    {
        Guard.IsNotEmpty(pizzas.Freeze(), nameof(pizzas));
        return pizzas;
    }
}