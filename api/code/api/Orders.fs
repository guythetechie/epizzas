namespace api

open Microsoft.AspNetCore.Http
open System
open System.Text.Json.Nodes
open Oxpecker
open FSharp.Control
open FSharpPlus
open common
open common.Serialization

type FindCosmosOrder = OrderId -> Async<Option<Order * ETag>>

type ListCosmosOrders = unit -> AsyncSeq<Order * ETag>

type CancelCosmosOrder = OrderId -> ETag -> Async<Result<unit, CosmosError>>

type CreateCosmosOrder = Order -> Async<Result<unit, CosmosError>>

type GetCurrentTime = unit -> DateTimeOffset

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

            let private getOrder findCosmosOrder orderId =
                async {
                    match! findCosmosOrder orderId with
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

            let private getResult findCosmosOrder orderIdString =
                apiOperation {
                    let! orderId = validateOrderId orderIdString
                    let! order, eTag = getOrder findCosmosOrder orderId
                    return getSuccessfulResponse order eTag
                }

            let get env orderIdString =
                async {
                    let (findCosmosOrder) = env

                    return! getResult findCosmosOrder orderIdString
                }
                |> EndpointHandler.fromResult

        [<RequireQualifiedAccess>]
        module ListOrders =
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

            let private getResult listCosmosOrders =
                apiOperation {
                    let! orders = listCosmosOrders () |> AsyncSeq.toListAsync
                    return getSuccessfulResponse orders
                }

            let get env =
                async {
                    let (listCosmosOrders) = env

                    return! getResult listCosmosOrders
                }
                |> EndpointHandler.fromResult

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

            let private cancelOrder cancelCosmosOrder orderId eTag =
                async {
                    match! cancelCosmosOrder orderId eTag with
                    | Ok() -> return Ok()
                    | Error error ->
                        match error with
                        | CosmosError.ETagMismatch ->
                            let message =
                                $"Order with id {OrderId.toString orderId} has been modified since it was retrieved. Please refresh the order and try again."

                            let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                            return Results.Json(json, statusCode = 412) |> Error
                        | CosmosError.NotFound ->
                            let message = $"Could not find order with id {OrderId.toString orderId}."
                            let json = JsonObject() |> JsonObject.setProperty "message" (implicit message)
                            return Results.NotFound(json) |> Error
                        | error ->
                            return
                                failwith
                                    $"Received unexpected error '{error}' when cancelling order with id {OrderId.toString orderId}."
                }

            let private getSuccesfulResponse () = Results.NoContent()

            let private getResult cancelCosmosOrder context orderIdString =
                apiOperation {
                    let! orderId = validateOrderId orderIdString
                    and! eTag = validateEtag context
                    do! cancelOrder cancelCosmosOrder orderId eTag
                    return getSuccesfulResponse ()
                }

            let get env orderIdString =
                let f context =
                    async {
                        let (cancelOrder) = env

                        return! getResult cancelOrder context orderIdString
                    }

                EndpointHandler.fromContext f

        [<RequireQualifiedAccess>]
        module CreateOrder =
            let private validateOrder getCurrentTime stream =
                let errorToResult (error: JsonError) =
                    let json =
                        let message = JsonError.getMessage error
                        JsonObject() |> JsonObject.setProperty "message" (implicit message)

                    let json =
                        match JsonError.getDetails error |> List.ofSeq with
                        | [] -> json
                        | details ->
                            json
                            |> JsonObject.setProperty
                                "details"
                                (details |> Seq.map JsonNode.op_Implicit |> JsonArray.fromSeq)

                    Results.BadRequest(json)

                async {
                    let! nodeResult = JsonNode.fromStream stream

                    let status =
                        OrderStatus.Created
                            {| By = "system"
                               Date = getCurrentTime () |}

                    return
                        nodeResult
                        |> bind JsonNode.asJsonObject
                        |> map (JsonObject.setProperty "status" (OrderStatus.serialize status))
                        |> bind Order.deserialize
                        |> JsonResult.toResult
                        |> Result.mapError errorToResult
                }

            let private createOrder createCosmosOrder order =
                async {
                    match! createCosmosOrder order with
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
                    let (getCurrentTime, createCosmosOrder) = env

                    let! order = validateOrder getCurrentTime stream
                    do! createOrder createCosmosOrder order
                    return getSuccessfulResponse ()
                }

            let get env =
                let f (context: HttpContext) = getResult env context.Request.Body

                EndpointHandler.fromContext f


    let getEndpoint env =
        let (findCosmosOrder: FindCosmosOrder,
             listCosmosOrders: ListCosmosOrders,
             cancelCosmosOrder: CancelCosmosOrder,
             createCosmosOrder: CreateCosmosOrder,
             getCurrentTime: GetCurrentTime) =
            env

        subRoute
            "/orders"
            [ GET
                  [ routef "/{%s}" (Handlers.GetOrder.get findCosmosOrder)
                    route "/" (Handlers.ListOrders.get listCosmosOrders) ]
              DELETE [ routef "/{%s}" (Handlers.CancelOrder.get cancelCosmosOrder) ]
              POST [ route "/" (Handlers.CreateOrder.get (getCurrentTime, createCosmosOrder)) ] ]
