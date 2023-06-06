using CommunityToolkit.Diagnostics;
using LanguageExt;

namespace EPizzas.Ordering.Common;

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
    public required ToppingAmount TypeAmount { get; init; }
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
    public required Seq<Topping> Toppings { get; init; }
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

public sealed record Order
{
    private readonly Seq<Pizza> pizzas;

    public required OrderId Id { get; init; }
    public Seq<Pizza> Pizzas { get => pizzas; init => pizzas = ValidatePizzas(value); }

    private static Seq<Pizza> ValidatePizzas(Seq<Pizza> pizzas)
    {
        Guard.IsNotEmpty(pizzas.Freeze(), nameof(pizzas));
        return pizzas;
    }
}