using LanguageExt;
using LanguageExt.Common;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;

namespace common;

public sealed record OrderId
{
    private readonly string value;

    private OrderId(string value) => this.value = value;

    public static Fin<OrderId> From(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Error.New("Order ID cannot be null or whitespace.")
            : new OrderId(value);

    public static OrderId FromOrThrow(string value) =>
        From(value).ThrowIfFail();

    public override string ToString() => value;

    public static JsonValue Serialize(OrderId orderId) =>
        JsonValue.Create(orderId.value);

    public static JsonResult<OrderId> Deserialize(JsonNode? json) =>
        from jsonValue in json.AsJsonValue()
        from orderIdString in jsonValue.AsString()
        from orderId in JsonResult.Lift(From(orderIdString))
        select orderId;
}

public abstract record OrderStatus
{
    public required DateTimeOffset Date { get; init; }
    public required string By { get; init; }

    public sealed record Created : OrderStatus
    {
        internal static JsonObject Serialize(Created status) =>
            new()
            {
                ["date"] = status.Date,
                ["by"] = status.By
            };

        internal static new JsonResult<Created> Deserialize(JsonNode? json) =>
            from jsonObject in json.AsJsonObject()
            let dateResult = from dateString in jsonObject.GetStringProperty("date")
                             from date in DateTimeOffset.TryParse(dateString, out var result)
                                            ? JsonResult.Succeed(result)
                                            : JsonResult.Fail<DateTimeOffset>($"{dateString} is not a valid date time offset.")
                             select date
            let byResult = jsonObject.GetStringProperty("by")
            from status in (dateResult, byResult)
                             .Apply((date, @by) => new Created
                             {
                                 Date = date,
                                 By = @by
                             })
                             .As()
            select status;
    }

    public sealed record Cancelled : OrderStatus
    {
        internal static JsonObject Serialize(Cancelled status) =>
            new()
            {
                ["date"] = status.Date,
                ["by"] = status.By
            };

        internal static new JsonResult<Cancelled> Deserialize(JsonNode? json) =>
            from jsonObject in json.AsJsonObject()
            let dateResult = from dateString in jsonObject.GetStringProperty("date")
                             from date in DateTimeOffset.TryParse(dateString, out var result)
                                            ? JsonResult.Succeed(result)
                                            : JsonResult.Fail<DateTimeOffset>($"{dateString} is not a valid date time offset.")
                             select date
            let byResult = jsonObject.GetStringProperty("by")
            from status in (dateResult, byResult)
                             .Apply((date, @by) => new Cancelled
                             {
                                 Date = date,
                                 By = @by
                             })
                             .As()
            select status;
    }

    public static JsonObject Serialize(OrderStatus status)
    {
        var (name, jsonObject) = status switch
        {
            Created created => (nameof(Created), Created.Serialize(created)),
            Cancelled cancelled => (nameof(Cancelled), Cancelled.Serialize(cancelled)),
            _ => throw new InvalidOperationException($"Order status {status.GetType()} is not supported.")
        };

        return jsonObject.SetProperty("name", name);
    }

    public static JsonResult<OrderStatus> Deserialize(JsonNode? json) =>
        from jsonObject in json.AsJsonObject()
        from name in jsonObject.GetStringProperty("name")
        from status in name switch
        {
            nameof(Created) => from status in Created.Deserialize(jsonObject)
                               select status as OrderStatus,
            nameof(Cancelled) => from status in Cancelled.Deserialize(jsonObject)
                                 select status as OrderStatus,
            _ => JsonResult.Fail<OrderStatus>($"Order status {name} is not supported.")
        }
        select status;
}

public sealed record Order
{
    private ImmutableArray<Pizza> pizzas;

    public required OrderId Id { get; init; }
    public required OrderStatus Status { get; init; }
    public required ImmutableArray<Pizza> Pizzas
    {
        get => pizzas;
        init => pizzas = value.IsEmpty
                          ? throw new InvalidOperationException("Cannot create an order with no pizzas.")
                          : value;
    }

    public static JsonObject Serialize(Order order) =>
        new()
        {
            ["orderId"] = OrderId.Serialize(order.Id),
            ["pizzas"] = order.Pizzas
                              .Select(Pizza.Serialize)
                              .ToJsonArray(),
            ["status"] = OrderStatus.Serialize(order.Status)
        };

    public static JsonResult<Order> Deserialize(JsonNode? json)
    {
        return from jsonObject in json.AsJsonObject()
               let orderIdResult = deserializeOrderId(jsonObject)
               let statusResult = deserializeStatus(jsonObject)
               let pizzasResult = deserializePizzas(jsonObject)
               from order in (orderIdResult, statusResult, pizzasResult)
                                .Apply((orderId, status, pizzas) => new Order
                                {
                                    Id = orderId,
                                    Status = status,
                                    Pizzas = pizzas
                                })
                                .As()
               select order;

        static JsonResult<OrderId> deserializeOrderId(JsonObject json) =>
            from property in json.GetProperty("orderId")
            from orderId in OrderId.Deserialize(property)
            select orderId;

        static JsonResult<OrderStatus> deserializeStatus(JsonObject json) =>
            from property in json.GetProperty("status")
            from status in OrderStatus.Deserialize(property)
            select status;

        static JsonResult<ImmutableArray<Pizza>> deserializePizzas(JsonObject json) =>
            from pizzasJsonArray in json.GetJsonArrayProperty("pizzas")
            from pizzas in from pizzas in pizzasJsonArray.AsIterable()
                                                         .Traverse(Pizza.Deserialize)
                                                         .As()
                           select pizzas.ToImmutableArray()
            from _ in pizzas.Length > 0
                        ? JsonResult.Succeed(Unit.Default)
                        : JsonResult.Fail<Unit>("Order must contain at least one pizza.")
            select pizzas;
    }
}