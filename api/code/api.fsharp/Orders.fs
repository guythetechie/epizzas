namespace api

open Microsoft.AspNetCore.Http
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

    let getEndpoint env =
        subRoute
            "/orders"
            [ GET
                  [ routef "/{%s}" (Handlers.GetOrder.get env)
                    route "/" (Handlers.ListOrders.get env) ]
              DELETE [ routef "/{%s}" (Handlers.CancelOrder.get env) ] ]
