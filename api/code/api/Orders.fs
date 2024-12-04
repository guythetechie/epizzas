namespace api

open Microsoft.AspNetCore.Http
open System
open System.IO
open System.Text.Json.Nodes
open Oxpecker
open FSharp.Control
open FSharpPlus
open common
open common.Serialization

type IFindCosmosOrder =
    abstract member FindCosmosOrder: OrderId -> Async<Option<Order * ETag>>

type IListCosmosOrders =
    abstract member ListCosmosOrders: unit -> AsyncSeq<Order * ETag>

type ICancelCosmosOrder =
    abstract member CancelCosmosOrder: OrderId -> ETag -> Async<Result<unit, CosmosError>>

type ICreateCosmosOrder =
    abstract member CreateCosmosOrder: Order -> Async<Result<unit, CosmosError>>

type IGetCurrentTime =
    abstract member GetCurrentTime: unit -> DateTimeOffset

[<RequireQualifiedAccess>]
module Orders =
    [<RequireQualifiedAccess>]
    module private Handlers =
        [<RequireQualifiedAccess>]
        module GetOrder =
            let private validateOrderId orderIdString =
                let errorToResult (message: string) =
                    let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)

                    Results.BadRequest(json)

                OrderId.fromString orderIdString |> Result.mapError errorToResult

            let private getOrder (env: #IFindCosmosOrder) orderId =
                async {
                    match! env.FindCosmosOrder orderId with
                    | Some(order, eTag) -> return Ok(order, eTag)
                    | None ->
                        let message = $"Could not find order with id {OrderId.toString orderId}."
                        let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                        return Results.NotFound(json) |> Error
                }

            let private getSuccessfulResponse order eTag =
                let json =
                    Order.serialize order
                    |> JsonObject.setProperty "eTag" (ETag.toString eTag |> implicit)

                Results.Ok(json)

            let private getResult env orderIdString =
                apiOperation {
                    let! orderId = validateOrderId orderIdString
                    let! order, eTag = getOrder env orderId
                    return getSuccessfulResponse order eTag
                }

            let get env orderIdString : EndpointHandler =
                fun context ->
                    task {
                        let! result = getResult env orderIdString |> Async.startAsTaskWithToken context.RequestAborted

                        return! context.Write result
                    }

        [<RequireQualifiedAccess>]
        module ListOrders =
            let private listOrders (env: #IListCosmosOrders) =
                env.ListCosmosOrders() |> AsyncSeq.toListAsync

            let private getSuccessfulResponse orders =
                let json =
                    let jsonArray =
                        orders
                        |> Seq.map (fun (order, eTag) ->
                            Order.serialize order
                            |> JsonObject.setProperty "eTag" (ETag.toString eTag |> implicit))
                        |> JsonArray.fromSeq

                    JsonObject() |> JsonObject.setProperty "values" jsonArray

                Results.Ok(json)

            let private getResult env =
                apiOperation {
                    let! orders = listOrders env
                    return getSuccessfulResponse orders
                }

            let get env : EndpointHandler =
                fun context ->
                    task {
                        let! result = getResult env |> Async.startAsTaskWithToken context.RequestAborted

                        return! context.Write result
                    }

        [<RequireQualifiedAccess>]
        module CancelOrder =
            let private validateOrderId orderIdString =
                let errorToResult (message: string) =
                    let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                    Results.BadRequest(json)

                OrderId.fromString orderIdString |> Result.mapError errorToResult

            let private validateEtag (context: HttpContext) =
                match context.TryGetHeaderValue "If-Match" with
                | Some eTagString ->
                    ETag.fromString eTagString
                    |> Result.mapError (fun message ->
                        let message = $"If-Match header value '{eTagString}' is not a valid ETag. {message}"
                        let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                        Results.BadRequest(json))
                | None ->
                    let json =
                        JsonObject()
                        |> JsonObject.setProperty "message" (implicit "If-Match header is required.")

                    Results.BadRequest(json) |> Error

            let private cancelOrder (env: #ICancelCosmosOrder) orderId eTag =
                env.CancelCosmosOrder orderId eTag
                |> map (
                    Result.mapError (function
                        | CosmosError.ETagMismatch ->
                            let message =
                                $"Order with id {OrderId.toString orderId} has been modified since it was retrieved. Please refresh the order and try again."

                            let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                            Results.Json(json, statusCode = 412)
                        | CosmosError.NotFound ->
                            let message = $"Could not find order with id {OrderId.toString orderId}."
                            let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                            Results.NotFound(json)
                        | error ->
                            failwith
                                $"Received unexpected error '{error}' when cancelling order with id {OrderId.toString orderId}.")
                )

            let private getSuccesfulResponse () = Results.NoContent()

            let private getResult env context orderIdString =
                apiOperation {
                    let! orderId = validateOrderId orderIdString
                    and! eTag = validateEtag context
                    do! cancelOrder env orderId eTag
                    return getSuccesfulResponse ()
                }

            let get env orderIdString : EndpointHandler =
                fun context ->
                    task {
                        let! result =
                            getResult env context orderIdString
                            |> Async.startAsTaskWithToken context.RequestAborted

                        return! context.Write result
                    }

        [<RequireQualifiedAccess>]
        module CreateOrder =
            let private validateOrder (env: #IGetCurrentTime) stream =
                let errorToResult (error: JsonError) =
                    let json =
                        JsonObject()
                        |> JsonObject.setProperty "message" (JsonNode.op_Implicit error.Message)

                    let json =
                        if Seq.isEmpty error.Details then
                            json
                        else
                            json
                            |> JsonObject.setProperty
                                "details"
                                (error.Details |> Seq.map JsonNode.op_Implicit |> JsonArray.fromSeq)

                    Results.BadRequest(json)

                async {
                    let! nodeResult = JsonNode.fromStream stream

                    let status =
                        OrderStatus.Created
                            {| By = "system"
                               Date = env.GetCurrentTime() |}

                    return
                        nodeResult
                        |> bind JsonNode.asJsonObject
                        |> map (JsonObject.setProperty "status" (OrderStatus.serialize status))
                        |> bind Order.deserialize
                        |> JsonResult.toResult
                        |> Result.mapError errorToResult
                }

            let private createOrder (env: #ICreateCosmosOrder) order =
                async {
                    match! env.CreateCosmosOrder order with
                    | Ok() -> return Ok()
                    | Error error ->
                        return
                            match error with
                            | CosmosError.AlreadyExists ->
                                let message = $"Order with id {OrderId.toString order.Id} already exists."
                                let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                                Results.Conflict(json) |> Error
                            | _ ->
                                failwith
                                    $"Received unexpected error '{error}' when creating order with id {OrderId.toString order.Id}."
                }

            let private getSuccessfulResponse () = Results.Created()

            let private getResult env stream =
                apiOperation {
                    let! order = validateOrder env stream
                    do! createOrder env order
                    return getSuccessfulResponse ()
                }

            let get env : EndpointHandler =
                fun context ->
                    task {
                        let! result =
                            getResult env context.Request.Body
                            |> Async.startAsTaskWithToken context.RequestAborted

                        return! context.Write result
                    }


    let getEndpoint env =
        subRoute
            "/orders"
            [ GET
                  [ routef "/{%s}" (Handlers.GetOrder.get env)
                    route "/" (Handlers.ListOrders.get env) ]
              DELETE [ routef "/{%s}" (Handlers.CancelOrder.get env) ]
              POST [ route "/" (Handlers.CreateOrder.get env) ] ]
