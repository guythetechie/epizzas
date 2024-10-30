using LanguageExt;
using LanguageExt.Common;
using System.Collections.Immutable;

namespace common;

public sealed record OrderId
{
    private OrderId(string value) => Value = value;

    public string Value { get; }

    public static Fin<OrderId> From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Error.New("Order ID cannot be null or whitespace.")
            : new OrderId(value);

    public static OrderId FromOrThrow(string value) =>
        From(value).ThrowIfFail();
}

public sealed record Order
{
    public required OrderId Id { get; init; }
    public required ImmutableArray<Pizza> Pizzas { get; init; }
}