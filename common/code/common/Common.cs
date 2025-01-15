using LanguageExt;
using LanguageExt.Common;
using System;

namespace common;

public sealed record ContinuationToken
{
    private readonly string value;

    private ContinuationToken(string value) => this.value = value;

    public static Fin<ContinuationToken> From(string? value) =>
        string.IsNullOrWhiteSpace(value)
        ? Error.New("Continuation token cannot be null or whitespace.")
        : new ContinuationToken(value);

    public override string ToString() => value;
}

public sealed record ETag
{
    private readonly string value;

    private ETag(string value) => this.value = value;

    public static Fin<ETag> From(string? value) =>
        string.IsNullOrWhiteSpace(value)
        ? Error.New("ETag cannot be null or whitespace.")
        : new ETag(value);

    public override string ToString() => value;

    public static ETag All { get; } = new("\"*\"");

    public static ETag Generate() => new($"\"{Guid.CreateVersion7()}\"");
}