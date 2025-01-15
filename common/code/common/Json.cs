using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using static LanguageExt.Prelude;

namespace common;

public static class JsonObjectModule
{
    public static JsonResult<JsonNode> GetProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject switch
        {
            null => JsonResult.Fail<JsonNode>("JSON object is null."),
            _ => jsonObject.TryGetPropertyValue(propertyName, out var jsonNode)
                    ? jsonNode switch
                    {
                        null => JsonResult.Fail<JsonNode>($"Property '{propertyName}' is null."),
                        _ => JsonResult.Succeed(jsonNode)
                    }
                    : JsonResult.Fail<JsonNode>($"JSON object does not have a property named '{propertyName}'.")
        };

    public static Option<JsonNode> GetOptionalProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Match(Option<JsonNode>.Some,
                         _ => Option<JsonNode>.None);

    public static JsonResult<T> GetProperty<T>(this JsonObject? jsonObject,
                                               string propertyName,
                                               Func<JsonNode, JsonResult<T>> selector) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(selector)
                  .AddPropertyNameToErrorMessage(propertyName);

    public static JsonResult<JsonObject> GetJsonObjectProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonObject());

    private static JsonResult<T> AddPropertyNameToErrorMessage<T>(this JsonResult<T> result, string propertyName)
    {
        return result.ReplaceError(replaceError);

        JsonError replaceError(JsonError error) =>
            JsonError.From($"Property '{propertyName}' is invalid. {error.Message}");
    }

    public static JsonResult<JsonArray> GetJsonArrayProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonArray());

    public static JsonResult<JsonValue> GetJsonValueProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonValue());

    public static JsonResult<string> GetStringProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonValue()
                                                   .Bind(jsonValue => jsonValue.AsString()));

    public static JsonResult<int> GetIntProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonValue()
                                                   .Bind(jsonValue => jsonValue.AsInt()));

    public static JsonResult<bool> GetBoolProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonValue()
                                                   .Bind(jsonValue => jsonValue.AsBool()));

    public static JsonResult<Guid> GetGuidProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonValue()
                                                   .Bind(jsonValue => jsonValue.AsGuid()));

    public static JsonResult<Uri> GetAbsoluteUriProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName,
                               jsonNode => jsonNode.AsJsonValue()
                                                   .Bind(jsonValue => jsonValue.AsAbsoluteUri()));

    public static JsonObject SetProperty(this JsonObject jsonObject, string propertyName, JsonNode? propertyValue)
    {
        jsonObject[propertyName] = propertyValue;
        return jsonObject;
    }

    public static JsonObject RemoveProperty(this JsonObject jsonObject, string propertyName)
    {
        jsonObject.Remove(propertyName);
        return jsonObject;
    }

    public static JsonResult<JsonObject> ToJsonObject(BinaryData? data, JsonSerializerOptions? options = default) =>
        JsonNodeModule.Deserialize<JsonObject>(data, options);
}

public static class JsonArrayModule
{
    public static JsonArray ToJsonArray(this IEnumerable<JsonNode?> nodes) =>
        new([.. nodes]);

    public static async ValueTask<JsonArray> ToJsonArray(this IAsyncEnumerable<JsonNode?> nodes,
                                                         CancellationToken cancellationToken) =>
        new([.. await nodes.ToListAsync(cancellationToken)]);

    public static JsonResult<ImmutableArray<JsonObject>> GetJsonObjects(this JsonArray jsonArray) =>
        jsonArray.GetElements(jsonNode => jsonNode.AsJsonObject(),
                              index => JsonError.From($"Node at index {index} is not a JSON object."));

    public static JsonResult<ImmutableArray<T>> GetElements<T>(this JsonArray jsonArray,
                                                               Func<JsonNode?, JsonResult<T>> selector,
                                                               Func<int, JsonError> errorFromIndex)
    {
        return jsonArray.Select((node, index) => (node, index))
                        .AsIterable()
                        .Traverse(x => nodeToElement(x.node, x.index))
                        .Map(iterable => iterable.ToImmutableArray())
                        .As();

        JsonResult<T> nodeToElement(JsonNode? node, int index) =>
            selector(node)
                .ReplaceError(_ => errorFromIndex(index));
    }

    public static JsonResult<ImmutableArray<JsonArray>> GetJsonArrays(this JsonArray jsonArray) =>
        jsonArray.GetElements(jsonNode => jsonNode.AsJsonArray(),
                              index => JsonError.From($"Node at index {index} is not a JSON array."));

    public static JsonResult<ImmutableArray<JsonValue>> GetJsonValues(this JsonArray jsonArray) =>
        jsonArray.GetElements(jsonNode => jsonNode.AsJsonValue(),
                              index => JsonError.From($"Node at index {index} is not a JSON value."));
}

public static class JsonNodeModule
{
    public static JsonResult<JsonObject> AsJsonObject(this JsonNode? node) =>
        node switch
        {
            JsonObject jsonObject => JsonResult.Succeed(jsonObject),
            null => JsonResult.Fail<JsonObject>("JSON node is null."),
            _ => JsonResult.Fail<JsonObject>("JSON node is not a JSON object.")
        };

    public static JsonResult<JsonArray> AsJsonArray(this JsonNode? node) =>
        node switch
        {
            JsonArray jsonArray => JsonResult.Succeed(jsonArray),
            null => JsonResult.Fail<JsonArray>("JSON node is null."),
            _ => JsonResult.Fail<JsonArray>("JSON node is not a JSON array.")
        };

    public static JsonResult<JsonValue> AsJsonValue(this JsonNode? node) =>
        node switch
        {
            JsonValue jsonValue => JsonResult.Succeed(jsonValue),
            null => JsonResult.Fail<JsonValue>("JSON node is null."),
            _ => JsonResult.Fail<JsonValue>("JSON node is not a JSON value.")
        };

    public static async ValueTask<JsonResult<JsonNode>> From(Stream? data,
                                                             JsonNodeOptions? nodeOptions = default,
                                                             JsonDocumentOptions documentOptions = default,
                                                             CancellationToken cancellationToken = default)
    {
        try
        {
            return data switch
            {
                null => JsonResult.Fail<JsonNode>("Binary data is null."),
                _ => await JsonNode.ParseAsync(data, nodeOptions, documentOptions, cancellationToken) switch
                {
                    null => JsonResult.Fail<JsonNode>("Deserialization returned a null result."),
                    var node => JsonResult.Succeed(node)
                }
            };
        }
        catch (JsonException exception)
        {
            var jsonError = JsonError.From(exception);
            return JsonResult.Fail<JsonNode>(jsonError);
        }
    }

    public static JsonResult<JsonNode> From(BinaryData? data, JsonNodeOptions? options = default)
    {
        try
        {
            return data switch
            {
                null => JsonResult.Fail<JsonNode>("Binary data is null."),
                _ => JsonNode.Parse(data, options) switch
                {
                    null => JsonResult.Fail<JsonNode>("Deserialization returned a null result."),
                    var node => JsonResult.Succeed(node)
                }
            };
        }
        catch (JsonException exception)
        {
            var jsonError = JsonError.From(exception);
            return JsonResult.Fail<JsonNode>(jsonError);
        }
    }

    public static JsonResult<T> Deserialize<T>(BinaryData? data, JsonSerializerOptions? options = default)
    {
        if (data is null)
        {
            return JsonResult.Fail<T>("Binary data is null.");
        }

        try
        {
            var jsonObject = JsonSerializer.Deserialize<T>(data, options ?? JsonSerializerOptions.Web);

            return jsonObject is null
                ? JsonResult.Fail<T>("Deserialization return a null result.")
                : JsonResult.Succeed(jsonObject);
        }
        catch (JsonException exception)
        {
            var jsonError = JsonError.From(exception);
            return JsonResult.Fail<T>(jsonError);
        }
    }

    public static Stream ToStream(JsonNode node, JsonSerializerOptions? options = default) =>
        BinaryData.FromObjectAsJson(node, options ?? JsonSerializerOptions.Web)
                  .ToStream();
}

public static class JsonValueModule
{
    public static JsonResult<string> AsString(this JsonValue? jsonValue) =>
        jsonValue?.GetValueKind() switch
        {
            JsonValueKind.String => jsonValue.GetStringValue() switch
            {
                null => JsonResult.Fail<string>("JSON value has a null string."),
                var stringValue => JsonResult.Succeed(stringValue)
            },
            _ => JsonResult.Fail<string>("JSON value is not a string.")
        };

    private static string? GetStringValue(this JsonValue? jsonValue) =>
        jsonValue?.GetValue<object>().ToString();

    public static JsonResult<int> AsInt(this JsonValue? jsonValue)
    {
        var errorMessage = "JSON value is not an integer.";

        return jsonValue?.GetValueKind() switch
        {
            JsonValueKind.Number => int.TryParse(jsonValue.GetStringValue(), out var result)
                                    ? JsonResult.Succeed(result)
                                    : JsonResult.Fail<int>(errorMessage),
            _ => JsonResult.Fail<int>(errorMessage)
        };
    }

    public static JsonResult<bool> AsBool(this JsonValue? jsonValue) =>
        jsonValue?.GetValueKind() switch
        {
            JsonValueKind.True => JsonResult.Succeed(true),
            JsonValueKind.False => JsonResult.Succeed(false),
            _ => JsonResult.Fail<bool>("JSON value is not a boolean.")
        };

    public static JsonResult<Guid> AsGuid(this JsonValue? jsonValue)
    {
        var errorMessage = "JSON value is not a GUID.";

        return jsonValue.AsString()
                        .Bind(stringValue => Guid.TryParse(jsonValue.GetStringValue(), out var result)
                                            ? JsonResult.Succeed(result)
                                            : JsonResult.Fail<Guid>(errorMessage))
                        .ReplaceError(errorMessage);
    }

    public static JsonResult<Uri> AsAbsoluteUri(this JsonValue? jsonValue)
    {
        var errorMessage = "JSON value is not an absolute URI.";

        return jsonValue.AsString()
                 .Bind(stringValue => Uri.TryCreate(jsonValue.GetStringValue(), UriKind.Absolute, out var result)
                                        ? JsonResult.Succeed(result)
                                        : JsonResult.Fail<Uri>(errorMessage))
                 .ReplaceError(errorMessage);
    }
}

public abstract record JsonError : Error, Semigroup<JsonError>
{
    public abstract FrozenSet<JsonError> Details { get; }

    public static JsonError From(string message) => NonExceptional.From(message);

    public static JsonError From(JsonException exception) => Exceptional.From(exception);

    public override bool HasException<T>() => typeof(T).IsAssignableFrom(typeof(JsonException));

    public JsonError Combine(JsonError rhs) =>
        (this, rhs) switch
        {
            (Exceptional left, Exceptional right) => left.Combine(right),
            (Exceptional left, _) => NonExceptional.From(left.Message).Combine(NonExceptional.From(rhs.Message)),
            (NonExceptional left, NonExceptional right) => left.Combine(right),
            (NonExceptional left, Exceptional right) => left.Combine(NonExceptional.From(right.Message)),
            _ => throw new UnreachableException()
        };

    private sealed record NonExceptional : JsonError, Semigroup<NonExceptional>
    {
        private readonly string message;
        private ImmutableHashSet<string> details = [];

        public override FrozenSet<JsonError> Details =>
            details.Select(message => (JsonError)NonExceptional.From(message))
                   .ToFrozenSet();

        private NonExceptional(string value) => this.message = value;

        public override string Message => message;

        public override bool IsExceptional => false;

        public override bool IsExpected => true;

        public override Exception ToException() => new JsonException(message);

        public override ErrorException ToErrorException() => ErrorException.New(ToException());

        public static new NonExceptional From(string value) => new(value);

        public NonExceptional Combine(NonExceptional rhs)
        {
            var error = From("Multiple errors have occurred. See details for more information.");
            error.details = rhs.details.ToImmutableArray() switch
            {
            [] => details.Add(rhs.message),
                var rhsDetails => details.Concat(rhsDetails).ToImmutableHashSet(details.KeyComparer)
            };
            return error;
        }
    }

    private sealed record Exceptional : JsonError, Semigroup<Exceptional>
    {
        private readonly JsonException exception;

        public override FrozenSet<JsonError> Details =>
            GetJsonExceptions(exception)
                .Select(exception => (JsonError)From(exception))
                .ToFrozenSet();

        private static ImmutableArray<JsonException> GetJsonExceptions(JsonException exception) =>
            exception.InnerException switch
            {
                AggregateException aggregateException =>
                    GetJsonExceptions(aggregateException) switch
                    {
                    [] => [exception],
                        var exceptions => exceptions
                    },
                _ => [exception]
            };

        private static ImmutableArray<JsonException> GetJsonExceptions(AggregateException exception) =>
            exception.Flatten()
                     .InnerExceptions
                     .Choose(exception => Optional(exception as JsonException))
                     .ToImmutableArray();

        private Exceptional(JsonException exception) => this.exception = exception;

        public override string Message => exception.Message;

        public override bool IsExceptional => true;

        public override bool IsExpected => false;

        public override Exception ToException() => exception;

        public override ErrorException ToErrorException() => ErrorException.New(exception);

        public static new Exceptional From(JsonException exception) => new(exception);

        public Exceptional Combine(Exceptional rhs) =>
            From(new("Multiple errors have occurred. See details for more information.",
                      new AggregateException(GetJsonExceptions(exception).Concat(GetJsonExceptions(rhs.exception)))));
    }
}

public class JsonResult :
    Monad<JsonResult>,
    Traversable<JsonResult>,
    Choice<JsonResult>
{
    public static JsonResult<T> Succeed<T>(T value) =>
        JsonResult<T>.Succeed(value);

    public static JsonResult<T> Fail<T>(JsonError error) =>
        JsonResult<T>.Fail(error);

    public static JsonResult<T> Fail<T>(string errorMessage) =>
        JsonResult<T>.Fail(JsonError.From(errorMessage));

    public static JsonResult<T> Lift<T>(Fin<T> fin) =>
        fin.Match(Succeed,
                  error => error switch
                  {
                      JsonError jsonError => Fail<T>(jsonError),
                      _ => Fail<T>(JsonError.From(error.Message))
                  });

    public static K<JsonResult, T> Pure<T>(T value) =>
        Succeed(value);

    public static K<JsonResult, T2> Bind<T1, T2>(K<JsonResult, T1> ma, Func<T1, K<JsonResult, T2>> f) =>
        ma.As()
          .Match(f, Fail<T2>);

    public static K<JsonResult, T2> Map<T1, T2>(Func<T1, T2> f, K<JsonResult, T1> ma) =>
        ma.As()
          .Match(t1 => Pure(f(t1)),
                 Fail<T2>);

    public static K<JsonResult, T2> Apply<T1, T2>(K<JsonResult, Func<T1, T2>> mf, K<JsonResult, T1> ma) =>
        mf.As()
          .Match(f => ma.Map(f),
                 error1 => ma.As()
                             .Match(t1 => Fail<T2>(error1),
                                    error2 => Fail<T2>(error1.Combine(error2))));

    public static K<JsonResult, T> Choose<T>(K<JsonResult, T> fa, K<JsonResult, T> fb) =>
        fa.As()
          .Match(_ => fa,
                 _ => fb);

    public static K<TApplicative, K<JsonResult, T2>> Traverse<TApplicative, T1, T2>(Func<T1, K<TApplicative, T2>> f,
                                                                                    K<JsonResult, T1> ta)
        where TApplicative : Applicative<TApplicative> =>
        ta.As()
          .Match(t1 => f(t1).Map(t1 => Succeed(t1).Kind()),
                 error => TApplicative.Pure(Fail<T2>(error).Kind()));

    public static S FoldWhile<A, S>(Func<A, Func<S, S>> f,
                                    Func<(S State, A Value), bool> predicate,
                                    S initialState,
                                    K<JsonResult, A> ta) =>
        ta.As()
          .Match(a => predicate((initialState, a))
                          ? f(a)(initialState)
                          : initialState,
                 _ => initialState);

    public static S FoldBackWhile<A, S>(Func<S, Func<A, S>> f,
                                        Func<(S State, A Value), bool> predicate,
                                        S initialState,
                                        K<JsonResult, A> ta) =>
        ta.As()
          .Match(a => predicate((initialState, a))
                          ? f(initialState)(a)
                          : initialState,
                 _ => initialState);
}

public class JsonResult<T> :
    IEquatable<JsonResult<T>>,
    K<JsonResult, T>
{
    private readonly Either<JsonError, T> value;

    private JsonResult(Either<JsonError, T> value) => this.value = value;

    public T2 Match<T2>(Func<T, T2> Succ, Func<JsonError, T2> Fail) =>
        value.Match(Fail, Succ);

    public Unit Match(Action<T> Succ, Action<JsonError> Fail) =>
        value.Match(Fail, Succ);

    public JsonResult<T2> Map<T2>(Func<T, T2> f) =>
        new(value.Map(f));

    public JsonResult<T2> Select<T2>(Func<T, T2> f) =>
        Map(f);

    public JsonResult<T2> SelectMany<T2>(Func<T, JsonResult<T2>> f) =>
        Bind(f);

    public JsonResult<TResult> SelectMany<T2, TResult>(Func<T, JsonResult<T2>> bind, Func<T, T2, TResult> project) =>
        Bind(t => bind(t).Map(t2 => project(t, t2)));

    public JsonResult<T2> Bind<T2>(Func<T, JsonResult<T2>> f) =>
        new(value.Bind(t => f(t).value));

    internal static JsonResult<T> Succeed(T value) =>
        new(value);

    internal static JsonResult<T> Fail(JsonError error) =>
        new(error);

    public override bool Equals(object? obj) =>
        obj is JsonResult<T> result && Equals(result);

    public override int GetHashCode() =>
        value.GetHashCode();

    public bool Equals(JsonResult<T>? other) =>
        other is not null
        && this.Match(t => other.Match(t2 => t?.Equals(t2) ?? false,
                                       _ => false),
                      error => other.Match(_ => false,
                                           error2 => error.Equals(error2)));
}

public static class JsonResultExtensions
{
    public static JsonResult<T> As<T>(this K<JsonResult, T> k) =>
        (JsonResult<T>)k;

    public static JsonResult<T> ReplaceError<T>(this JsonResult<T> result, string newErrorMessage) =>
        result.ReplaceError(JsonError.From(newErrorMessage));

    public static JsonResult<T> ReplaceError<T>(this JsonResult<T> result, JsonError newError) =>
        result.ReplaceError(_ => newError);

    public static JsonResult<T> ReplaceError<T>(this JsonResult<T> result, Func<JsonError, JsonError> f) =>
        result.Match(_ => result,
                     error => JsonResult.Fail<T>(f(error)));

    public static T IfFail<T>(this JsonResult<T> result, Func<JsonError, T> f) =>
        result.Match(t => t, f);

    public static T? DefaultIfFail<T>(this JsonResult<T> result) =>
        result.Match<T?>(t => t, _ => default);

    public static T ThrowIfFail<T>(this JsonResult<T> result) =>
        result.IfFail(error => throw error.ToException());

    public static Fin<T> ToFin<T>(this K<JsonResult, T> k) =>
        k.As().Match(Fin<T>.Succ, Fin<T>.Fail);

    public static Either<L, R> ToEither<L, R>(this K<JsonResult, R> k, Func<JsonError, L> mapError) =>
        k.As().Match(Either<L, R>.Right, error => Either<L, R>.Left(mapError(error)));
}