﻿using System.Collections.Frozen;

namespace common;

public abstract record PizzaTopping
{
    public sealed record Cheese : PizzaTopping
    {
        public static Cheese Instance { get; } = new();
    }

    public sealed record Pepperoni : PizzaTopping
    {
        public static Pepperoni Instance { get; } = new();
    }

    public sealed record Sausage : PizzaTopping
    {
        public static Sausage Instance { get; } = new();
    }
}

public abstract record PizzaToppingAmount
{
    public sealed record Light : PizzaToppingAmount
    {
        public static Light Instance { get; } = new();
    }

    public sealed record Normal : PizzaToppingAmount
    {
        public static Normal Instance { get; } = new();
    }

    public sealed record Extra : PizzaToppingAmount
    {
        public static Extra Instance { get; } = new();
    }
}

public abstract record PizzaSize
{
    public sealed record Small : PizzaSize
    {
        public static Small Instance { get; } = new();
    }

    public sealed record Medium : PizzaSize
    {
        public static Medium Instance { get; } = new();
    }

    public sealed record Large : PizzaSize
    {
        public static Large Instance { get; } = new();
    }
}

public sealed record Pizza
{
    public required PizzaSize Size { get; init; }
    public required FrozenDictionary<PizzaTopping, PizzaToppingAmount> Toppings { get; init; }
}