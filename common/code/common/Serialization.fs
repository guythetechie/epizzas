namespace common.Serialization

open common
open System.Text.Json.Nodes
open FSharpPlus

module PizzaToppingKind =
    let serialize value =
        PizzaToppingKind.toString value |> JsonValue.Create |> Unchecked.nonNull

    let deserialize json =
        monad {
            let! jsonValue = JsonNode.asJsonValue json
            let! stringValue = JsonValue.asString jsonValue

            match PizzaToppingKind.fromString stringValue with
            | Ok pizzaToppingKind -> return pizzaToppingKind
            | Error error -> return! JsonResult.failWithMessage error
        }

module PizzaToppingAmount =
    let serialize value =
        PizzaToppingAmount.toString value |> JsonValue.Create |> Unchecked.nonNull

    let deserialize json =
        monad {
            let! jsonValue = JsonNode.asJsonValue json
            let! stringValue = JsonValue.asString jsonValue

            match PizzaToppingAmount.fromString stringValue with
            | Ok pizzaToppingAmount -> return pizzaToppingAmount
            | Error error -> return! JsonResult.failWithMessage error
        }

module PizzaSize =
    let serialize value =
        PizzaSize.toString value |> JsonValue.Create |> Unchecked.nonNull

    let deserialize json =
        monad {
            let! jsonValue = JsonNode.asJsonValue json
            let! stringValue = JsonValue.asString jsonValue

            match PizzaSize.fromString stringValue with
            | Ok pizzaSize -> return pizzaSize
            | Error error -> return! JsonResult.failWithMessage error
        }

module Pizza =
    let serializeTopping kind amount =
        JsonObject()
        |> JsonObject.setProperty "kind" (PizzaToppingKind.serialize kind)
        |> JsonObject.setProperty "amount" (PizzaToppingAmount.serialize amount)

    let serializeToppings toppings =
        toppings
        |> Map.toSeq
        |> Seq.map (uncurry serializeTopping)
        |> JsonArray.fromSeq

    let serialize (pizza: Pizza) =
        JsonObject()
        |> JsonObject.setProperty "size" (PizzaSize.serialize pizza.Size)
        |> JsonObject.setProperty "toppings" (serializeToppings pizza.Toppings)

    let deserializeTopping json =
        monad {
            let! jsonObject = JsonNode.asJsonObject json

            let! kind =
                jsonObject
                |> JsonObject.getPropertyFromResult PizzaToppingKind.deserialize "kind"

            and! amount =
                jsonObject
                |> JsonObject.getPropertyFromResult PizzaToppingAmount.deserialize "amount"

            return kind, amount
        }

    let deserializeToppings json =
        monad {
            let! jsonArray = JsonNode.asJsonArray json
            let! toppings = jsonArray |> List.ofSeq |> traverse deserializeTopping
            return Map.ofSeq toppings
        }

    let deserialize json =
        monad {
            let! jsonObject = JsonNode.asJsonObject json

            let! size = jsonObject |> JsonObject.getPropertyFromResult PizzaSize.deserialize "size"

            and! toppings = jsonObject |> JsonObject.getPropertyFromResult deserializeToppings "toppings"

            return
                { Pizza.Size = size
                  Toppings = toppings }
        }

module OrderId =
    let serialize value =
        OrderId.toString value |> JsonValue.Create |> Unchecked.nonNull

    let deserialize json =
        JsonNode.asJsonValue json
        |> bind JsonValue.asString
        |> bind (fun stringValue ->
            OrderId.fromString stringValue
            |> Result.either JsonResult.succeed JsonResult.failWithMessage)

module OrderStatus =
    let serialize status =
        match status with
        | Created created ->
            JsonObject()
            |> JsonObject.setProperty "status" (JsonValue.Create(nameof (Created)))
            |> JsonObject.setProperty "by" (JsonValue.Create created.By)
            |> JsonObject.setProperty "date" (JsonValue.Create created.Date)
        | Cancelled cancelled ->
            JsonObject()
            |> JsonObject.setProperty "status" (JsonValue.Create(nameof (Cancelled)))
            |> JsonObject.setProperty "by" (JsonValue.Create cancelled.By)
            |> JsonObject.setProperty "date" (JsonValue.Create cancelled.Date)

    let deserialize json =
        let deserializeUserProperty =
            JsonNode.asJsonValue >> JsonResult.bind JsonValue.asString

        let deserializeDateProperty =
            JsonNode.asJsonValue >> JsonResult.bind JsonValue.asDateTimeOffset

        monad {
            let! jsonObject = JsonNode.asJsonObject json

            let! status =
                jsonObject
                |> JsonObject.getProperty "status"
                |> bind JsonNode.asJsonValue
                |> bind JsonValue.asString

            match status with
            | nameof (Created) ->
                let! by = jsonObject |> JsonObject.getPropertyFromResult deserializeUserProperty "by"

                and! date = jsonObject |> JsonObject.getPropertyFromResult deserializeDateProperty "date"

                return Created {| By = by; Date = date |}
            | nameof (Cancelled) ->
                let! by = jsonObject |> JsonObject.getPropertyFromResult deserializeUserProperty "by"

                and! date = jsonObject |> JsonObject.getPropertyFromResult deserializeDateProperty "date"

                return Cancelled {| By = by; Date = date |}
            | _ -> return! JsonResult.failWithMessage $"'{status}' is not a valid order status."
        }

module Order =
    let serialize (order: Order) =
        JsonObject()
        |> JsonObject.setProperty "orderId" (OrderId.serialize order.Id)
        |> JsonObject.setProperty "status" (OrderStatus.serialize order.Status)
        |> JsonObject.setProperty "pizzas" (order.Pizzas |> Seq.map Pizza.serialize |> JsonArray.fromSeq)

    let deserialize json : JsonResult<Order> =
        monad {
            let! jsonObject = JsonNode.asJsonObject json
            let! id = jsonObject |> JsonObject.getPropertyFromResult OrderId.deserialize "orderId"
            and! status = jsonObject |> JsonObject.getPropertyFromResult OrderStatus.deserialize "status"

            and! pizzas =
                let deserializePizzasProperty =
                    JsonNode.asJsonArray
                    >> bind (traverse Pizza.deserialize)
                    >> map List.ofSeq
                    >> bind (function
                        | [] -> JsonResult.failWithMessage "Order must have at least one pizza."
                        | pizzas -> JsonResult.succeed pizzas)

                jsonObject
                |> JsonObject.getPropertyFromResult deserializePizzasProperty "pizzas"

            return
                { Id = id
                  Status = status
                  Pizzas = pizzas }
        }
