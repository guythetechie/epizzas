using LanguageExt;

namespace common;

public sealed record OrderId : NonEmptyString
{
    public OrderId(string value) : base(value) { }
}

public sealed record Order
{
    public required OrderId Id { get; init; }
    public required Seq<Pizza> Pizzas { get; init; }
}