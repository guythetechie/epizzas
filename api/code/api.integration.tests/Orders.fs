[<RequireQualifiedAccess>]
module api.integration.tests.Orders

open Bogus
open Bogus.DataSets
open System
open System.Net
open System.Net.Http
open System.Text.Json.Nodes
open FsCheck
open FsCheck.FSharp
open FSharpPlus
open FSharp.Control
open Faqt
open common
open common.Serialization

[<RequireQualifiedAccess>]
module private Gen =
    let pizzaToppingKind =
        Gen.elements
            [ PizzaToppingKind.Cheese
              PizzaToppingKind.Pepperoni
              PizzaToppingKind.Sausage ]

    let pizzaToppingAmount =
        Gen.elements
            [ PizzaToppingAmount.Light
              PizzaToppingAmount.Normal
              PizzaToppingAmount.Extra ]

    let pizzaSize = Gen.elements [ PizzaSize.Small; PizzaSize.Medium; PizzaSize.Large ]

    let pizzaTopping = Gen.zip pizzaToppingKind pizzaToppingAmount

    let pizzaToppings = Gen.mapOf pizzaToppingKind pizzaToppingAmount

    let pizza =
        gen {
            let! size = pizzaSize
            let! toppings = pizzaToppings

            return
                { Pizza.Size = size
                  Toppings = toppings }
        }

    let private randomizer = Gen.default'<int> () |> Gen.map Randomizer

    let private internet =
        randomizer |> Gen.map (fun randomizer -> Internet(Random = randomizer))

    let private userName = internet |> Gen.map _.UserName()

    let orderId =
        Gen.default'<Guid> ()
        |> Gen.map string
        |> Gen.map OrderId.fromString
        |> Gen.map (Result.defaultWith (fun error -> failwith error))

    let orderStatus =
        Gen.oneof
            [ gen {
                  let! by = userName
                  let! date = Gen.default'<DateTimeOffset> ()
                  return OrderStatus.Created {| By = by; Date = date |}
              }

              gen {
                  let! by = userName
                  let! date = Gen.default'<DateTimeOffset> ()
                  return OrderStatus.Cancelled {| By = by; Date = date |}
              } ]

    let order =
        gen {
            let! orderId = orderId
            let! status = orderStatus
            let! pizzas = Gen.listOf pizza

            return
                { Order.Id = orderId
                  Status = status
                  Pizzas = pizzas }
        }

    let invalidPizzaSizeJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonValue
              Gen.JsonNode.jsonValue
              |> Gen.filter (fun value ->
                  match string value with
                  | nameof (PizzaSize.Small)
                  | nameof (PizzaSize.Medium)
                  | nameof (PizzaSize.Large) -> false
                  | _ -> true) ]
        |> Gen.nullable

    let invalidPizzaToppingKindJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonValue
              Gen.JsonNode.jsonValue
              |> Gen.filter (fun value ->
                  match string value with
                  | nameof (PizzaToppingKind.Cheese)
                  | nameof (PizzaToppingKind.Pepperoni)
                  | nameof (PizzaToppingKind.Sausage) -> false
                  | _ -> true) ]
        |> Gen.nullable

    let invalidPizzaToppingAmountJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonValue
              Gen.JsonNode.jsonValue
              |> Gen.filter (fun value ->
                  match string value with
                  | nameof (PizzaToppingAmount.Light)
                  | nameof (PizzaToppingAmount.Normal)
                  | nameof (PizzaToppingAmount.Extra) -> false
                  | _ -> true) ]
        |> Gen.nullable

    let private modifyJsonGen f (json: #JsonNode) =
        Gen.constant (json) |> Gen.map (fun json -> f json :> JsonNode)

    let private removeJsonPropertyGen name jsonObject =
        jsonObject |> modifyJsonGen (JsonObject.removeProperty name)

    let private setJsonPropertyGen name propertyGen jsonObject =
        propertyGen
        |> Gen.bind (fun property ->
            modifyJsonGen (fun jsonObject -> JsonObject.setProperty name property jsonObject) jsonObject)

    let invalidPizzaToppingKindAmountJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonObject
              Gen.JsonNode.jsonObject
              gen {
                  let! topping = pizzaTopping
                  let json = topping |> uncurry Pizza.serializeTopping

                  return!
                      Gen.oneof
                          [ removeJsonPropertyGen "kind" json
                            removeJsonPropertyGen "amount" json
                            setJsonPropertyGen "kind" invalidPizzaToppingKindJson json
                            setJsonPropertyGen "amount" invalidPizzaToppingAmountJson json ]
              } ]
        |> Gen.nullable

    let invalidPizzaToppings =
        Gen.oneof
            [ Gen.JsonNode.nonJsonArray
              gen {
                  let! toppings = pizzaToppings |> Gen.filter (fun map -> not map.IsEmpty)

                  let toppingsList =
                      toppings
                      |> Map.toList
                      |> List.map (uncurry Pizza.serializeTopping)
                      |> List.map (fun json -> json :> JsonNode)

                  let! index = Gen.choose (0, List.length toppingsList - 1)
                  let! invalidTopping = invalidPizzaToppingKindAmountJson
                  let toppingsList = List.setAt index invalidTopping toppingsList
                  return JsonArray.fromSeq toppingsList
              } ]
        |> Gen.nullable

    let invalidPizzaJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonObject
              Gen.JsonNode.jsonObject
              gen {
                  let! pizza = pizza
                  let json = pizza |> Pizza.serialize

                  return!
                      Gen.oneof
                          [ removeJsonPropertyGen "size" json
                            removeJsonPropertyGen "toppings" json
                            setJsonPropertyGen "size" invalidPizzaSizeJson json
                            setJsonPropertyGen "toppings" invalidPizzaToppings json ]
              } ]
        |> Gen.nullable

    let invalidPizzasJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonArray
              Gen.constant (JsonArray() :> JsonNode)
              gen {
                  let! pizzas = Gen.nonEmptyListOf pizza

                  let pizzasList =
                      pizzas |> List.map Pizza.serialize |> List.map (fun json -> json :> JsonNode)

                  let! index = Gen.choose (0, List.length pizzas - 1)
                  let! invalidPizza = invalidPizzaJson
                  let pizzasList = List.setAt index invalidPizza pizzasList
                  return JsonArray.fromSeq pizzasList
              } ]
        |> Gen.nullable

    let invalidOrderIdJson =
        Gen.oneof [ Gen.JsonNode.nonJsonValue; Gen.constant (JsonNode.op_Implicit "") ]
        |> Gen.nullable

    let invalidOrderJson =
        Gen.oneof
            [ Gen.JsonNode.nonJsonObject
              Gen.JsonNode.jsonObject
              gen {
                  let! order = order
                  let json = order |> Order.serialize

                  return!
                      Gen.oneof
                          [ removeJsonPropertyGen "orderId" json
                            removeJsonPropertyGen "pizzas" json
                            setJsonPropertyGen "orderId" invalidOrderIdJson json
                            setJsonPropertyGen "pizzas" invalidPizzasJson json ]
              } ]
        |> Gen.nullable

[<RequireQualifiedAccess>]
module private Check =
    let fromGenWithConfig config gen f =
        let arb = Arb.fromGen gen
        let property = Prop.forAll arb (f >> ignore)
        Check.One(config, property)

    let fromGenWithRuns count gen f =
        let config = Config.QuickThrowOnFailure.WithMaxTest count
        fromGenWithConfig config gen f

    let fromGen gen f =
        let config = Config.QuickThrowOnFailure
        fromGenWithConfig config gen f

let private getResponseJson (response: HttpResponseMessage) =
    async {
        let! cancellationToken = Async.CancellationToken
        use! stream = response.Content.ReadAsStreamAsync(cancellationToken) |> Async.AwaitTask
        return! JsonNode.fromStream stream
    }

let private ``Check that there are no orders`` getClient =
    async {
        use! response = Api.listOrders getClient
        response.Should().HaveStatusCode(HttpStatusCode.OK) |> ignore

        let! result = getResponseJson response

        let orders =
            monad {
                let! json = result
                let! jsonObject = JsonNode.asJsonObject json
                let! values = JsonObject.getJsonArrayProperty "values" jsonObject
                let! (orders: Order seq) = values |> traverse Order.deserialize
                return orders
            }
            |> JsonResult.throwIfFail

        orders.Should().BeEmpty() |> ignore
    }

let private ``Check that invalid JSON order cannot be created`` getClient =
    Check.fromGenWithRuns 10 Gen.invalidOrderIdJson (fun json ->
        async {
            use! response = Api.createOrder getClient json
            response.Should().HaveStatusCode(HttpStatusCode.BadRequest) |> ignore
        }
        |> Async.RunSynchronously)

let private ``Check that order does not exist`` getClient orderId =
    async {
        use! response = Api.getOrder getClient (OrderId.toString orderId)
        response.Should().HaveStatusCode(HttpStatusCode.NotFound) |> ignore
    }

let private ``Check that order can be created`` getClient order =
    async {
        let orderJson = Order.serialize order
        use! response = Api.createOrder getClient orderJson
        response.Should().BeSuccessful() |> ignore
        
        use! response = Api.getOrder getClient (OrderId.toString order.Id)
        response.Should().BeSuccessful() |> ignore
        let! responseJsonResult = getResponseJson  response
        responseJsonResult.Should().BeSuccess() |> ignore
    }

let private testOrder getClient (order: Order) =
    async {
        do! ``Check that order does not exist`` getClient order.Id
        do! ``Check that order can be created`` getClient order
    }

let private testOrders getClient =
    Check.fromGenWithRuns 10 Gen.order (testOrder getClient >> Async.RunSynchronously)

let test env =
    async {
        let container, getClient = env

        do! Cosmos.emptyContainer container

        do! ``Check that there are no orders`` getClient

        ``Check that invalid JSON order cannot be created`` getClient

        do testOrders getClient

        do! Cosmos.emptyContainer container
    }
