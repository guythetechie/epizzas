using CommunityToolkit.Diagnostics;
using LanguageExt;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api;

// Implementation from Azure.Core library
public sealed record ETag
{
    private const string QuoteString = "\"";
    private const string WeakETagPrefix = "W/\"";

    public string Value { get; }

    /// <summary>
    /// Creates a new instance of <see cref="ETag"/>.
    /// </summary>
    /// <param name="etag">The string value of the ETag.</param>
    public ETag(string etag)
    {
        Guard.IsNotNull(etag, nameof(etag));

        Value = FormatETag(etag);
    }

    /// <summary>
    /// Instance of the wildcard <see cref="ETag"/>.
    /// </summary>
    public static readonly ETag All = new("*");

    public override string ToString()
    {
        return Value;
    }

    private static string FormatETag(string eTag)
    {
        return IsValidQuotedFormat(eTag)
                ? eTag
                : $"{QuoteString}{eTag}{QuoteString}";
    }

    private static bool IsValidQuotedFormat(string value)
    {
        return (value.StartsWith(QuoteString, StringComparison.Ordinal)
                || value.StartsWith(WeakETagPrefix, StringComparison.Ordinal)) &&
            value.EndsWith(QuoteString, StringComparison.Ordinal);
    }
}

public sealed record ContinuationToken
{
    public ContinuationToken(string value)
    {
        Guard.IsNotNullOrWhiteSpace(value, nameof(value));

        Value = value;
    }

    public string Value { get; }
}

public abstract record ErrorCode
{
    public sealed record ResourceNotFound : ErrorCode;
    public sealed record ResourceAlreadyExists : ErrorCode;
    public sealed record InvalidConditionalHeader : ErrorCode;
    public sealed record InvalidJsonBody : ErrorCode;
    public sealed record InvalidId : ErrorCode;
    public sealed record ETagMismatch : ErrorCode;
    public sealed record InternalServerError : ErrorCode;
    public sealed record InvalidContinuationToken : ErrorCode;
}

internal static class HttpIResultExtensions
{
    public static async ValueTask<IResult> Coalesce<TError, TSuccess>(this Either<TError, ValueTask<TSuccess>> either) where TSuccess : IResult where TError : IResult
    {
        return await either.MatchAsync<IResult>(async right => await right,
                                                left => left);
    }
}

internal static class ValidationExtensions
{
    public static T IfFailThrowJsonException<T>(this Validation<string, T> validation)
    {
        return validation.IfFail(errors => throw ValidationErrorsToJsonException(errors));
    }

    private static JsonException ValidationErrorsToJsonException(IEnumerable<string> errors)
    {
        return errors.ToArray() switch
        {
            [] => throw new InvalidOperationException(),
            [var error] => new JsonException(error),
            _ => new JsonException("An error has occurred.",
                                   new AggregateException(errors.Map(error => new JsonException(error))))
        };
    }
}