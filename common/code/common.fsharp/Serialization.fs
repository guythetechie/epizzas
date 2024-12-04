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
    let serialize (pizza: Pizza) =
        JsonObject()
        |> JsonObject.setProperty "size" (PizzaSize.serialize pizza.Size)
        |> JsonObject.setProperty
            "toppings"
            (pizza.Toppings
             |> Map.toSeq
             |> Seq.map (fun (kind, amount) ->
                 JsonObject()
                 |> JsonObject.setProperty "kind" (PizzaToppingKind.serialize kind)
                 |> JsonObject.setProperty "amount" (PizzaToppingAmount.serialize amount))
             |> JsonArray.fromSeq)

    let deserialize json =
        let deserializeToppingKindAmount json =
            monad {
                let! jsonObject = JsonNode.asJsonObject json

                let! kind =
                    jsonObject
                    |> JsonObject.getProperty "kind"
                    |> JsonResult.bind PizzaToppingKind.deserialize

                and! amount =
                    jsonObject
                    |> JsonObject.getProperty "amount"
                    |> JsonResult.bind PizzaToppingAmount.deserialize

                return kind, amount
            }

        let deserializeToppings json =
            monad {
                let! jsonArray = JsonNode.asJsonArray json
                let! toppings = jsonArray |> List.ofSeq |> traverse deserializeToppingKindAmount
                return List.toSeq toppings
            }

        monad {
            let! jsonObject = JsonNode.asJsonObject json

            let! size =
                jsonObject
                |> JsonObject.getProperty "size"
                |> JsonResult.bind PizzaSize.deserialize

            and! toppings =
                jsonObject
                |> JsonObject.getProperty "toppings"
                |> JsonResult.bind deserializeToppings
                |> map Map.ofSeq

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
        monad {
            let! jsonObject = JsonNode.asJsonObject json

            let! status =
                jsonObject
                |> JsonObject.getProperty "status"
                |> bind JsonNode.asJsonValue
                |> bind JsonValue.asString

            match status with
            | nameof (Created) ->
                let! by =
                    jsonObject
                    |> JsonObject.getProperty "by"
                    |> bind JsonNode.asJsonValue
                    |> bind JsonValue.asString

                and! date =
                    jsonObject
                    |> JsonObject.getProperty "date"
                    |> bind JsonNode.asJsonValue
                    |> bind JsonValue.asDateTimeOffset

                return Created {| By = by; Date = date |}
            | nameof (Cancelled) ->
                let! by =
                    jsonObject
                    |> JsonObject.getProperty "by"
                    |> bind JsonNode.asJsonValue
                    |> bind JsonValue.asString

                and! date =
                    jsonObject
                    |> JsonObject.getProperty "date"
                    |> bind JsonNode.asJsonValue
                    |> bind JsonValue.asDateTimeOffset

                return Cancelled {| By = by; Date = date |}
            | _ -> return! JsonResult.failWithMessage $"'{status}' is not a valid order status."
        }

module Order =
    let serialize (order: Order) =
        JsonObject()
        |> JsonObject.setProperty "id" (OrderId.serialize order.Id)
        |> JsonObject.setProperty "status" (OrderStatus.serialize order.Status)
        |> JsonObject.setProperty "pizzas" (order.Pizzas |> Seq.map Pizza.serialize |> JsonArray.fromSeq)

    let deserialize json : JsonResult<Order> =
        monad {
            let! jsonObject = JsonNode.asJsonObject json
            let! id = jsonObject |> JsonObject.getProperty "id" |> bind OrderId.deserialize
            and! status = jsonObject |> JsonObject.getProperty "status" |> bind OrderStatus.deserialize

            and! pizzas =
                jsonObject
                |> JsonObject.getProperty "pizzas"
                |> bind JsonNode.asJsonArray
                |> bind (traverse Pizza.deserialize)

            return
                { Id = id
                  Status = status
                  Pizzas = pizzas }
        }
