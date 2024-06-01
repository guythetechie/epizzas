using System;

namespace common;

public abstract record NonEmptyString
{
    protected NonEmptyString(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        Value = value;
    }

    public string Value { get; }

    public virtual bool Equals(NonEmptyString? other) => string.Equals(Value, other?.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);

    public sealed override string ToString() => Value;
}

public sealed record ContinuationToken : NonEmptyString
{
    public ContinuationToken(string value) : base(value) { }
}