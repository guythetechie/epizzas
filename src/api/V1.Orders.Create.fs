namespace V1.Orders.Create

open Microsoft.AspNetCore.Http
open System
open System.Linq
open System.Text.Json.Nodes
open Microsoft.Extensions.DependencyInjection
open FSharpPlus
open FSharpPlus.Data
open Giraffe
open Giraffe.EndpointRouting
open common
open V1.Orders.Common

type private CreateOrder = Order -> Async<Result<unit, ApiErrorCode>>

[<RequireQualifiedAccess>]
module Services =
    let private configureCreateOrder =
        Func<IServiceProvider, CreateOrder>(fun provider ->
            fun order ->
                let container = provider.GetRequiredService<OrdersCosmosContainer>()

                async {
                    match! Cosmos.createOrder container order with
                    | Ok _ -> return Ok()
                    | Error CosmosError.ResourceAlreadyExists -> return Error ApiErrorCode.ResourceAlreadyExists
                    | Error error -> return invalidOp $"Error '{error}' is unexpected." |> raise
                })

    let configure (services: IServiceCollection) =
        services.AddSingleton<CreateOrder>(configureCreateOrder) |> ignore

[<RequireQualifiedAccess>]
module Endpoints =
    let private getSuccessfulResult requestUri orderId =
        let locationUri = Uri.appendPathToUri requestUri (OrderId.toString orderId)
        TypedResults.Created(locationUri) :> IResult

    let private createOrder (f: CreateOrder) order =
        async {
            match! f order with
            | Ok _ -> return Ok()
            | Error ApiErrorCode.ResourceAlreadyExists ->
                return
                    Results.Conflict(
                        {| code = ApiErrorCode.toString ApiErrorCode.ResourceAlreadyExists
                           message = $"Order with ID '{OrderId.toString order.Id}' already exists." |}
                    )
                    |> Error
            | Error error -> return invalidOp $"Error '{error}' is unexpected." |> raise
        }

    let private validateToppingAmount toppingJson =
        JsonObject.tryGetStringProperty toppingJson "amount"
        |> Result.bind (fun amount ->
            if
                [ nameof ToppingAmount.Light
                  nameof ToppingAmount.Normal
                  nameof ToppingAmount.Extra ]
                    .Contains(amount, StringComparer.OrdinalIgnoreCase)
            then
                ToppingAmount.fromString amount |> Ok
            else
                Error $"'{amount}' is not a valid topping amount.")
        |> Validation.liftResult List.singleton

    let private validateToppingKind toppingJson =
        JsonObject.tryGetStringProperty toppingJson "kind"
        |> Result.bind (fun kind ->
            if
                [ nameof ToppingKind.Cheese
                  nameof ToppingKind.Pepperoni
                  nameof ToppingKind.Sausage ]
                    .Contains(kind, StringComparer.OrdinalIgnoreCase)
            then
                ToppingKind.fromString kind |> Ok
            else
                Error $"'{kind}' is not a valid topping kind.")
        |> Validation.liftResult List.singleton

    let private validateTopping toppingJson =
        applicative {
            let! kind = validateToppingKind toppingJson
            and! amount = validateToppingAmount toppingJson

            return kind, amount
        }

    let private validateToppingJsonNode (toppingJsonNode: JsonNode) =
        match toppingJsonNode with
        | :? JsonObject as toppingJson -> validateTopping toppingJson
        | _ -> Validation.Failure [ "Pizza topping must be a JSON object." ]

    let private validateToppingJsonNodes toppingsJsonArray =
        match List.ofSeq toppingsJsonArray with
        | [] -> Validation.Failure [ "At least one topping must be added." ]
        | toppings -> traverse validateToppingJsonNode toppings
        |> Validation.map Map.ofSeq

    let private validatePizzaToppings pizzaJson =
        JsonObject.tryGetJsonArrayProperty pizzaJson "toppings"
        |> Validation.liftResult List.singleton
        |> Validation.bind validateToppingJsonNodes

    let private validatePizzaSize pizzaJson =
        JsonObject.tryGetStringProperty pizzaJson "size"
        |> Result.bind (fun size ->
            if
                [ nameof PizzaSize.Small; nameof PizzaSize.Medium; nameof PizzaSize.Large ]
                    .Contains(size, StringComparer.OrdinalIgnoreCase)
            then
                PizzaSize.fromString size |> Ok
            else
                Error $"'{size}' is not a valid pizza size.")
        |> Validation.liftResult List.singleton

    let private validatePizza pizzaJson =
        applicative {
            let! size = validatePizzaSize pizzaJson
            and! toppings = validatePizzaToppings pizzaJson

            return { Size = size; Toppings = toppings }
        }

    let private validatePizzaJsonNode (pizzaJsonNode: JsonNode) =
        match pizzaJsonNode with
        | :? JsonObject as pizzaJson -> validatePizza pizzaJson
        | _ -> Validation.Failure [ "Pizza must be a JSON object." ]

    let private validatePizzaJsonNodes pizzaJsonArray =
        match List.ofSeq pizzaJsonArray with
        | [] -> Validation.Failure [ "At least one pizza must be ordered." ]
        | pizzas -> traverse validatePizzaJsonNode pizzas

    let private validatePizzas orderJson =
        JsonObject.tryGetJsonArrayProperty orderJson "pizzas"
        |> Validation.liftResult List.singleton
        |> Validation.bind validatePizzaJsonNodes

    let private validateOrderId orderJson =
        JsonObject.tryGetStringProperty orderJson "id"
        |> Result.bind (fun id ->
            if String.IsNullOrWhiteSpace id then
                Error "Order ID cannot be empty."
            else
                OrderId.fromString id |> Ok)
        |> Validation.liftResult List.singleton

    let private validateOrder orderJson =
        applicative {
            let! orderId = validateOrderId orderJson
            and! pizzas = validatePizzas orderJson

            return
                { Order.Id = orderId
                  Status = OrderStatus.Pending
                  Pizzas = pizzas }
        }

    let private tryGetOrderFromJson orderJson =
        validateOrder orderJson
        |> Validation.toResult
        |> Result.mapError (fun errors ->
            Results.BadRequest
                {| code = ApiErrorCode.toString ApiErrorCode.InvalidRequestBody
                   message = "Request body is invalid."
                   details = Seq.ofList errors |})

    let private tryGetOrderJson context = HttpContext.tryGetJsonObject context

    let private handle (context: HttpContext) =
        let createOrder = createOrder (context.GetService<CreateOrder>())
        let requestUri = HttpContext.getRequestUri context

        apiOperation {
            let! orderJson = tryGetOrderJson context
            let! order = tryGetOrderFromJson orderJson
            let! _ = createOrder order


            return getSuccessfulResult requestUri order.Id
        }
        |> ApiOperation.toHttpHandler

    let private handler: HttpHandler = fun next context -> (handle context) next context

    let list = [ POST [ route "/" handler ] ]
