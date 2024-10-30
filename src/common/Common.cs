using LanguageExt;
using LanguageExt.Common;
using System;
using System.Diagnostics.CodeAnalysis;

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

public interface IDomainType<T, TRepresentation>
{
    public TRepresentation Value { get; }

    public abstract static Fin<T> From(TRepresentation value);
}

public interface IIdentifierDomainType<T, TRepresentation> : IDomainType<T, TRepresentation>, IEquatable<T>;

public sealed record ContinuationToken
{
    private readonly string value;

    private ContinuationToken(string value) => this.value = value;

    public static Fin<ContinuationToken> From(string? value) =>
        string.IsNullOrWhiteSpace(value)
        ? Error.New("Continuation token cannot be null or whitespace.")
        : new ContinuationToken(value);

#pragma warning disable CS8777 // Parameter must have a non-null value when exiting.
    public static ContinuationToken FromOrThrow([NotNull] string? value) =>
        From(value).ThrowIfFail();
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    public override string ToString() => value;
}