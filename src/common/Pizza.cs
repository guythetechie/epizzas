using LanguageExt;

namespace common;

public abstract record PizzaToppingKind
{
    public sealed record Cheese : PizzaToppingKind
    {
        public static Cheese Instance { get; } = new();

        public override string ToString() => nameof(Cheese);
    }

    public sealed record Pepperoni : PizzaToppingKind
    {
        public static Pepperoni Instance { get; } = new();

        public override string ToString() => nameof(Pepperoni);
    }

    public sealed record Sausage : PizzaToppingKind
    {
        public static Sausage Instance { get; } = new();

        public override string ToString() => nameof(Sausage);
    }
}

public abstract record PizzaToppingAmount
{
    public sealed record Light : PizzaToppingAmount
    {
        public static Light Instance { get; } = new();

        public override string ToString() => nameof(Light);
    }

    public sealed record Normal : PizzaToppingAmount
    {
        public static Normal Instance { get; } = new();

        public override string ToString() => nameof(Normal);
    }

    public sealed record Extra : PizzaToppingAmount
    {
        public static Extra Instance { get; } = new();

        public override string ToString() => nameof(Extra);
    }
}

public abstract record PizzaSize
{
    public sealed record Small : PizzaSize
    {
        public static Small Instance { get; } = new();

        public override string ToString() => nameof(Small);
    }

    public sealed record Medium : PizzaSize
    {
        public static Medium Instance { get; } = new();

        public override string ToString() => nameof(Medium);
    }

    public sealed record Large : PizzaSize
    {
        public static Large Instance { get; } = new();

        public override string ToString() => nameof(Large);
    }
}

public sealed record Pizza
{
    public required PizzaSize Size { get; init; }
    public required HashMap<PizzaToppingKind, PizzaToppingAmount> Toppings { get; init; }
}