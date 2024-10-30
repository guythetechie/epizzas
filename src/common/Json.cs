using LanguageExt;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace common;

public sealed record JsonError : Error
{
    private JsonError(string message) => Message = message;

    public override string Message { get; }

    public override bool IsExceptional { get; }

    public override bool IsExpected { get; } = true;

    public override ErrorException ToErrorException() => ErrorException.New(new JsonException(Message));

    public static JsonError From(string message) => new(message);
}

public static class JsonArrayExtensions
{
    public static JsonArray ToJsonArray(this IEnumerable<JsonNode?> nodes) =>
        new([.. nodes]);

    public static ValueTask<JsonArray> ToJsonArray(this IAsyncEnumerable<JsonNode?> nodes, CancellationToken cancellationToken) =>
        nodes.AggregateAsync(new JsonArray(),
                            (array, node) => new JsonArray([.. array, node]),
                            cancellationToken);

    public static ImmutableArray<JsonObject> GetJsonObjects(this JsonArray jsonArray) =>
        jsonArray.Choose(node => node.AsJsonObject().ToOption())
                 .ToImmutableArray();

    public static ImmutableArray<JsonArray> GetJsonArrays(this JsonArray jsonArray) =>
        jsonArray.Choose(node => node.AsJsonArray().ToOption())
                 .ToImmutableArray();

    public static ImmutableArray<JsonValue> GetJsonValues(this JsonArray jsonArray) =>
        jsonArray.Choose(node => node.AsJsonValue().ToOption())
                 .ToImmutableArray();

    public static ImmutableArray<string> GetNonEmptyOrWhitespaceStrings(this JsonArray jsonArray) =>
        jsonArray.Choose(node => node.AsString().ToOption())
                 .Where(value => !string.IsNullOrWhiteSpace(value))
                 .ToImmutableArray();
}

public static class JsonNodeExtensions
{
    public static JsonNodeOptions Options { get; } = new() { PropertyNameCaseInsensitive = true };

    public static Fin<JsonObject> AsJsonObject(this JsonNode? node) =>
        node is JsonObject jsonObject
            ? jsonObject
            : JsonError.From("JSON node is not a JSON object.");

    public static Fin<JsonArray> AsJsonArray(this JsonNode? node) =>
        node is JsonArray jsonArray
            ? jsonArray
            : JsonError.From("JSON node is not a JSON array.");

    public static Fin<JsonValue> AsJsonValue(this JsonNode? node) =>
        node is JsonValue jsonValue
            ? jsonValue
            : JsonError.From("JSON node is not a JSON value.");

    public static Fin<string> AsString(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsString);

    public static Fin<Guid> AsGuid(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsGuid);

    public static Fin<Uri> AsAbsoluteUri(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsAbsoluteUri);

    public static Fin<DateTimeOffset> AsDateTimeOffset(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsDateTimeOffset);

    public static Fin<DateTime> AsDateTime(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsDateTime);

    public static Fin<int> AsInt(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsInt);

    public static Fin<double> AsDouble(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsDouble);

    public static Fin<bool> AsBool(this JsonNode? node) =>
        node.AsJsonValue()
            .Bind(JsonValueExtensions.AsBool);
}

public static class JsonObjectExtensions
{
    public static JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static Option<JsonNode> GetOptionalProperty(this JsonObject jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .ToOption();

    public static Fin<JsonNode> GetProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject is null
            ? JsonError.From("JSON object is null.")
            : jsonObject.TryGetPropertyValue(propertyName, out var node)
                ? node is null
                    ? JsonError.From($"Property '{propertyName}' is null.")
                    : node
                : JsonError.From($"Property '{propertyName}' is missing.");

    public static Fin<JsonObject> GetJsonObjectProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsJsonObject()
                                    .ReplaceError($"Property '{propertyName}' is not a JSON object."));

    private static Fin<T> ReplaceError<T>(this Fin<T> fin, string errorMessage) =>
        fin.ReplaceError(() => JsonError.From(errorMessage));

    public static Fin<JsonArray> GetJsonArrayProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsJsonArray()
                                    .ReplaceError($"Property '{propertyName}' is not a JSON array."));

    public static Fin<JsonValue> GetJsonValueProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsJsonValue()
                                    .ReplaceError($"Property '{propertyName}' is not a JSON value."));

    public static Fin<string> GetStringProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsString()
                                    .ReplaceError($"Property '{propertyName}' is not a string."));

    public static Fin<string> GetNonEmptyOrWhiteSpaceStringProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetStringProperty(propertyName)
                  .Bind<string>(value => string.IsNullOrWhiteSpace(value)
                                            ? JsonError.From($"Property '{propertyName}' is empty or whitespace.")
                                            : value);

    public static Fin<Guid> GetGuidProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsGuid()
                                    .ReplaceError($"Property '{propertyName}' is not a GUID."));

    public static Fin<Uri> GetAbsoluteUriProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsAbsoluteUri()
                                    .ReplaceError($"Property '{propertyName}' is not an absolute URI."));

    public static Fin<DateTimeOffset> GetDateTimeOffsetProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsDateTimeOffset()
                                    .ReplaceError($"Property '{propertyName}' is not a datetime offset."));

    public static Fin<DateTime> GetDateTimeProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsDateTime()
                                    .ReplaceError($"Property '{propertyName}' is not a datetime."));

    public static Fin<int> GetIntProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsInt()
                                    .ReplaceError($"Property '{propertyName}' is not an integer."));

    public static Fin<double> GetDoubleProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsDouble()
                                    .ReplaceError($"Property '{propertyName}' is not a double."));

    public static Fin<bool> GetBoolProperty(this JsonObject? jsonObject, string propertyName) =>
        jsonObject.GetProperty(propertyName)
                  .Bind(node => node.AsBool()
                                    .ReplaceError($"Property '{propertyName}' is not a boolean."));

    [return: NotNullIfNotNull(nameof(jsonObject))]
    public static JsonObject? SetProperty(this JsonObject? jsonObject, string propertyName, JsonNode? jsonNode)
    {
        if (jsonObject is null)
        {
            return null;
        }
        else
        {
            jsonObject[propertyName] = jsonNode;
            return jsonObject;
        }
    }

    /// <summary>
    /// Sets <paramref name="jsonObject"/>[<paramref name="propertyName"/>] = <paramref name="jsonNode"/> if <paramref name="jsonNode"/> is not null.
    /// </summary>
    [return: NotNullIfNotNull(nameof(jsonObject))]
    public static JsonObject? SetPropertyIfNotNull(this JsonObject? jsonObject, string propertyName, JsonNode? jsonNode) =>
        jsonNode is null
            ? jsonObject
            : jsonObject.SetProperty(propertyName, jsonNode);

    /// <summary>
    /// Sets <paramref name="jsonObject"/>'s property <paramref name="propertyName"/> to the value of <paramref name="option"/> if <paramref name="option"/> is Some.
    /// </summary>
    [return: NotNullIfNotNull(nameof(jsonObject))]
    public static JsonObject? SetPropertyIfSome(this JsonObject? jsonObject, string propertyName, Option<JsonNode> option) =>
        jsonObject.SetPropertyIfNotNull(propertyName, option.ValueUnsafe());

    [return: NotNullIfNotNull(nameof(jsonObject))]
    public static JsonObject? RemoveProperty(this JsonObject? jsonObject, string propertyName)
    {
        if (jsonObject is null)
        {
            return null;
        }
        else
        {
            jsonObject.Remove(propertyName);
            return jsonObject;
        }
    }

    private static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static JsonObject Parse<T>(T obj) =>
        TryParse(obj)
            .IfFail(_ => throw new JsonException($"Could not parse {typeof(T).Name} as a JSON object."));

    public static Fin<JsonObject> TryParse<T>(T obj) =>
        JsonSerializer.SerializeToNode(obj, serializerOptions)
                      .AsJsonObject();
}

public static class JsonValueExtensions
{
    public static Fin<string> AsString(this JsonValue? jsonValue) =>
        jsonValue is not null && jsonValue.TryGetValue<string>(out var value)
            ? value
            : JsonError.From("JSON value is not a string.");

    public static Fin<Guid> AsGuid(this JsonValue? jsonValue) =>
        jsonValue is not null && jsonValue.TryGetValue<Guid>(out var guid)
            ? guid
            : jsonValue.AsString()
                       .Bind(x => Guid.TryParse(x, out var guidFromString)
                                    ? Prelude.FinSucc(guidFromString)
                                    : JsonError.From("JSON value is not a GUID."));

    public static Fin<Uri> AsAbsoluteUri(this JsonValue? jsonValue) =>
        jsonValue.AsString()
                 .Bind(x => Uri.TryCreate(x, UriKind.Absolute, out var uri)
                                ? Prelude.FinSucc(uri)
                                : JsonError.From("JSON value is not an absolute URI."));

    public static Fin<DateTimeOffset> AsDateTimeOffset(this JsonValue? jsonValue) =>
        jsonValue is not null && jsonValue.TryGetValue<DateTimeOffset>(out var dateTimeOffset)
            ? dateTimeOffset
            : jsonValue.AsString()
                       .Bind(x => DateTimeOffset.TryParse(x, out var dateTime)
                                    ? Prelude.FinSucc(dateTime)
                                    : JsonError.From("JSON value is not a datetime offset."));

    public static Fin<DateTime> AsDateTime(this JsonValue? jsonValue) =>
        jsonValue is not null && jsonValue.TryGetValue<DateTime>(out var dateTime)
            ? dateTime
            : jsonValue.AsString()
                       .Bind(x => DateTime.TryParse(x, out var dateTime)
                                    ? Prelude.FinSucc(dateTime)
                                    : JsonError.From("JSON value is not a datetime."));

    public static Fin<int> AsInt(this JsonValue? jsonValue) =>
        jsonValue is not null && jsonValue.TryGetValue<int>(out var value)
            ? value
            : JsonError.From("JSON value is not an int.");

    public static Fin<double> AsDouble(this JsonValue? jsonValue) =>
    jsonValue is not null && jsonValue.TryGetValue<double>(out var value)
        ? value
        : JsonError.From("JSON value is not a double.");

    public static Fin<bool> AsBool(this JsonValue? jsonValue) =>
        jsonValue is not null && jsonValue.TryGetValue<bool>(out var value)
            ? value
            : JsonError.From("JSON value is not a bool.");
}