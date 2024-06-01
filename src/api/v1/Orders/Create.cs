using common;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;

namespace api.v1.Orders;

internal sealed record ResourceAlreadyExists
{
    public static ResourceAlreadyExists Instance { get; } = new();
}

internal delegate EitherT<ResourceAlreadyExists, IO, Unit> CreateOrder(Order order);

internal static class CreateEndpoints
{
    public static void Map(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/", Handle);
    }

    private static IResult Handle([FromServices] CreateOrder createOrder, [FromBody] JsonNode? body, CancellationToken cancellationToken)
    {
        var operation = from order in ApiOperation.LiftEither(TryGetOrder(body))
                        from _ in ApiOperation.LiftEither(CreateOrder(order, createOrder))
                        from successfulResponse in ApiOperation.Pure(GetSuccessfulResponse())
                        select successfulResponse;

        return operation.Run(cancellationToken);
    }

    private static Either<IResult, Order> TryGetOrder(JsonNode? body) =>
        ValidateOrder(body)
            .ToEither()
            .MapLeft(error => Results.BadRequest(new
            {
                code = ApiErrorCode.InvalidRequestBody.Instance.ToString(),
                message = error.Count switch
                {
                    0 => throw new InvalidOperationException("Must have at least one error."),
                    1 => error.Head.Message,
                    _ => "Request body is invalid."
                },
                details = error is ManyErrors manyErrors
                          ? manyErrors.Errors
                                      .Select(error => error.Message)
                                      .ToSeq()
                          : []
            }));

    private static Validation<Error, Order> ValidateOrder(JsonNode? body)
    {
        static Validation<Error, JsonObject> validateBodyIsJsonObject(JsonNode? body) =>
            body is JsonObject jsonObject
            ? jsonObject
            : Error.New("Request body must be a JSON object.");

        static Validation<Error, Order> validateBodyJsonObject(JsonObject body) =>
            (ValidateOrderId(body), ValidatePizzas(body))
                .Apply((orderId, pizzas) => new Order
                {
                    Id = orderId,
                    Pizzas = pizzas
                })
                .As();

        return from jsonObject in validateBodyIsJsonObject(body)
               from order in validateBodyJsonObject(jsonObject)
               select order;
    }

    private static Validation<Error, OrderId> ValidateOrderId(JsonObject body)
    {
        static Validation<Error, string> validateBodyHasOrderIdString(JsonObject body) =>
            body.TryGetStringProperty("id")
                .ToValidation();

        static Validation<Error, OrderId> validateOrderIdString(string orderIdString) =>
            string.IsNullOrWhiteSpace(orderIdString)
            ? Error.New("Order ID cannot be empty.")
            : new OrderId(orderIdString);

        return from orderIdString in validateBodyHasOrderIdString(body)
               from orderId in validateOrderIdString(orderIdString)
               select orderId;
    }

    private static Validation<Error, Seq<Pizza>> ValidatePizzas(JsonObject body)
    {
        static Validation<Error, JsonArray> validateBodyHasPizzasJsonArray(JsonObject body) =>
            body.TryGetJsonArrayProperty("pizzas")
                .ToValidation();

        static Validation<Error, Seq<Pizza>> validatePizzasJsonArray(JsonArray jsonArray) =>
            jsonArray.AsEnumerableM()
                     .Traverse(ValidatePizza)
                     .Map(pizzas => pizzas.ToSeq())
                     .As();

        static Validation<Error, Seq<Pizza>> validateAtLeastOnePizzaExists(Seq<Pizza> pizzas) =>
            pizzas.Count == 0
            ? Error.New("Order must have at least one pizza.")
            : pizzas;

        return from jsonArray in validateBodyHasPizzasJsonArray(body)
               from pizzas in validatePizzasJsonArray(jsonArray)
               from _ in validateAtLeastOnePizzaExists(pizzas)
               select pizzas;
    }

    private static Validation<Error, Pizza> ValidatePizza(JsonNode? pizzaJson)
    {
        static Validation<Error, JsonObject> validatePizzaJsonIsJsonObject(JsonNode? pizzaJson) =>
            pizzaJson is JsonObject jsonObject
            ? jsonObject
            : Error.New("Pizza must be a JSON object.");

        static Validation<Error, Pizza> validatePizzaJsonObject(JsonObject jsonObject) =>
            (ValidatePizzaSize(jsonObject), ValidatePizzaToppings(jsonObject))
                .Apply((size, toppings) => new Pizza
                {
                    Size = size,
                    Toppings = toppings
                })
                .As();

        return from jsonObject in validatePizzaJsonIsJsonObject(pizzaJson)
               from pizza in validatePizzaJsonObject(jsonObject)
               select pizza;
    }

    private static Validation<Error, PizzaSize> ValidatePizzaSize(JsonObject pizzaJson) =>
        pizzaJson.TryGetStringProperty("size")
                 .Bind<string>(size => string.IsNullOrWhiteSpace(size) ? Prelude.Fail("Pizza size cannot be empty.") : Prelude.Pure(size))
                 .Bind<PizzaSize>(size => size switch
                 {
                     _ when size.Equals(nameof(PizzaSize.Large), StringComparison.OrdinalIgnoreCase) => PizzaSize.Large.Instance,
                     _ when size.Equals(nameof(PizzaSize.Medium), StringComparison.OrdinalIgnoreCase) => PizzaSize.Medium.Instance,
                     _ when size.Equals(nameof(PizzaSize.Small), StringComparison.OrdinalIgnoreCase) => PizzaSize.Small.Instance,
                     _ => Prelude.Fail($"'{size}' is not a valid pizza size.")
                 })
                 .ToValidation();

    private static Validation<Error, HashMap<PizzaToppingKind, PizzaToppingAmount>> ValidatePizzaToppings(JsonObject pizzaJson)
    {
        static Validation<Error, JsonArray> validatePizzaJsonContainsToppingsProperty(JsonObject pizzaJson) =>
            pizzaJson.TryGetJsonArrayProperty("toppings")
                     .ToValidation();

        static Validation<Error, Seq<(PizzaToppingKind, PizzaToppingAmount)>> validatePizzaToppingsJsonArray(JsonArray jsonArray) =>
            jsonArray.AsEnumerableM()
                     .Traverse(ValidatePizzaTopping)
                     .Map(toppings => toppings.ToSeq())
                     .As();

        return from jsonArray in validatePizzaJsonContainsToppingsProperty(pizzaJson)
               from toppings in validatePizzaToppingsJsonArray(jsonArray)
               from toppingsDictionary in ValidatePizzaToppingsDictionary(toppings)
               select toppingsDictionary;
    }

    private static Validation<Error, (PizzaToppingKind, PizzaToppingAmount)> ValidatePizzaTopping(JsonNode? toppingJson)
    {
        static Validation<Error, JsonObject> validateToppingJsonIsJsonObject(JsonNode? toppingJson) =>
            toppingJson is JsonObject jsonObject
            ? jsonObject
            : Error.New("Pizza topping must be a JSON object.");

        static Validation<Error, (PizzaToppingKind, PizzaToppingAmount)> validateToppingJsonObject(JsonObject jsonObject) =>
            (ValidatePizzaToppingKind(jsonObject), ValidatePizzaToppingAmount(jsonObject))
                .Apply((kind, amount) => (kind, amount))
                .As();

        return from jsonObject in validateToppingJsonIsJsonObject(toppingJson)
               from topping in validateToppingJsonObject(jsonObject)
               select topping;
    }

    private static Validation<Error, PizzaToppingKind> ValidatePizzaToppingKind(JsonObject toppingJson) =>
        toppingJson.TryGetStringProperty("kind")
                   .Bind<PizzaToppingKind>(kind => kind switch
                   {
                       _ when kind.Equals(nameof(PizzaToppingKind.Cheese), StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Cheese.Instance,
                       _ when kind.Equals(nameof(PizzaToppingKind.Pepperoni), StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Pepperoni.Instance,
                       _ when kind.Equals(nameof(PizzaToppingKind.Sausage), StringComparison.OrdinalIgnoreCase) => PizzaToppingKind.Sausage.Instance,
                       _ => Prelude.Fail($"'{kind}' is not a valid pizza topping kind.")
                   })
                   .ToValidation();

    private static Validation<Error, PizzaToppingAmount> ValidatePizzaToppingAmount(JsonObject toppingJson) =>
        toppingJson.TryGetStringProperty("amount")
                   .Bind<PizzaToppingAmount>(amount => amount switch
                   {
                       _ when amount.Equals(nameof(PizzaToppingAmount.Light), StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Light.Instance,
                       _ when amount.Equals(nameof(PizzaToppingAmount.Normal), StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Normal.Instance,
                       _ when amount.Equals(nameof(PizzaToppingAmount.Extra), StringComparison.OrdinalIgnoreCase) => PizzaToppingAmount.Extra.Instance,
                       _ => Prelude.Fail($"'{amount}' is not a valid pizza topping amount.")
                   })
                   .ToValidation();

    private static Validation<Error, HashMap<PizzaToppingKind, PizzaToppingAmount>> ValidatePizzaToppingsDictionary(Seq<(PizzaToppingKind, PizzaToppingAmount)> toppings)
    {
        static Validation<Error, Seq<(PizzaToppingKind, PizzaToppingAmount)>> validateAtLeastOneToppingExists(Seq<(PizzaToppingKind, PizzaToppingAmount)> toppings) =>
            toppings switch
            {
            [] => Error.New("Pizza must have at least one topping."),
                var toppingsArray => toppingsArray
            };

        static Validation<Error, Seq<(PizzaToppingKind, PizzaToppingAmount)>> validateNoDuplicateToppingKinds(Seq<(PizzaToppingKind, PizzaToppingAmount)> toppings) =>
            toppings.GroupBy(x => x.Item1)
                    .Where(@group => @group.Count() > 1)
                    .Select(@group => @group.Key)
                    .AsEnumerableM()
                    .ToSeq() switch
            {
            [] => toppings,
                var duplicates => Error.New($"Pizza cannot have duplicate topping kinds. Found duplicates {string.Join(", ", duplicates)}")
            };

        return from _ in validateAtLeastOneToppingExists(toppings)
               from __ in validateNoDuplicateToppingKinds(toppings)
               select toppings.ToHashMap();
    }

    private static EitherT<IResult, IO, Unit> CreateOrder(Order order, CreateOrder createOrder) =>
        createOrder(order)
            .MapLeft(_ => Results.Conflict(new
            {
                code = ApiErrorCode.ResourceAlreadyExists.Instance.ToString(),
                message = $"Order with ID {order.Id} already exists."
            }));

    private static IResult GetSuccessfulResponse() =>
        Results.NoContent();
}

internal static class CreateServices
{
    public static void Configure(IServiceCollection services)
    {
        Services.ConfigureOrdersCosmosContainer(services);

        services.TryAddSingleton(CreateOrder);
    }

    private static CreateOrder CreateOrder(IServiceProvider provider)
    {
        var activitySource = provider.GetRequiredService<ActivitySource>();
        var container = provider.GetRequiredService<OrdersCosmosContainer>();

        return order =>
        {
            using var _ = activitySource.StartActivity(nameof(CreateOrder));

            var json = Cosmos.SerializeOrder(order);
            var partitionKey = new PartitionKey(order.Id.Value);

            return CosmosModule.CreateRecord(container.Value, json, partitionKey)
                               .MapLeft(_ => ResourceAlreadyExists.Instance);
        };
    }
}