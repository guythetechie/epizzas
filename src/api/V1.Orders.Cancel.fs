namespace V1.Orders.Cancel

open Azure
open Microsoft.AspNetCore.Http
open System
open System.Net
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.EndpointRouting
open common
open V1.Orders.Common

type private FindStatus = OrderId -> Async<OrderStatus option>

type private SetStatusToCanceled = OrderId -> ETag -> Async<Result<unit, ApiErrorCode>>

[<RequireQualifiedAccess>]
module Services =
    let private configureSetStatusToCanceled =
        Func<IServiceProvider, SetStatusToCanceled>(fun provider ->
            fun orderId etag ->
                let container = provider.GetRequiredService<OrdersCosmosContainer>()

                async {
                    match! Cosmos.tryUpdateOrderStatus container orderId etag OrderStatus.Canceled with
                    | Ok _ -> return Ok()
                    | Error CosmosError.ResourceNotFound -> return Error ApiErrorCode.ResourceNotFound
                    | Error CosmosError.PreconditionFailed -> return Error ApiErrorCode.PreconditionFailed
                    | Error error -> return invalidOp $"Error '{error}' is unexpected." |> raise
                })

    let private configureFindStatus =
        Func<IServiceProvider, FindStatus>(fun provider ->
            fun orderId ->
                let container = provider.GetRequiredService<OrdersCosmosContainer>()
                Cosmos.findOrderStatus container orderId)

    let configure (services: IServiceCollection) =
        services.AddSingleton<FindStatus>(configureFindStatus) |> ignore

        services.AddSingleton<SetStatusToCanceled>(configureSetStatusToCanceled)
        |> ignore

[<RequireQualifiedAccess>]
module Endpoints =
    let private validateOrderId orderIdString =
        if String.IsNullOrWhiteSpace orderIdString then
            Results.BadRequest
                {| code = ApiErrorCode.toString InvalidRequestParameter
                   message = "Order ID cannot be empty." |}
            |> Error
        else
            OrderId.fromString orderIdString |> Ok

    let private tryGetETag context =
        match HttpContext.getIfMatchHeaders context |> List.ofSeq with
        | [] -> Error "If-Match header is missing."
        | [ etag ] -> AzureETag.fromString etag |> Ok
        | _ -> Error "Multiple If-Match headers are not allowed."
        |> Result.mapError (fun message ->
            Results.BadRequest
                {| code = ApiErrorCode.toString ApiErrorCode.InvalidHeader
                   message = message |})

    let private checkStatus (findStatus: FindStatus) orderId =
        async {
            match! findStatus orderId with
            | Some OrderStatus.Canceled -> return Results.NoContent() |> Choice2Of2
            | _ -> return Choice1Of2()
        }

    let private cancelOrder (setStatusToCanceled: SetStatusToCanceled) orderId eTag =
        async {
            match! setStatusToCanceled orderId eTag with
            | Ok() -> return Results.NoContent()
            | Error ApiErrorCode.ResourceNotFound ->
                return
                    Results.NotFound
                        {| code = ApiErrorCode.toString ApiErrorCode.ResourceNotFound
                           message = $"Order with ID '{OrderId.toString orderId}' was not found." |}
            | Error ApiErrorCode.PreconditionFailed ->
                return
                    Results.Json(
                        {| code = ApiErrorCode.toString ApiErrorCode.PreconditionFailed
                           message =
                            $"The provided ETag for order '{OrderId.toString orderId}' no longer valid. Another resource might have changed the order. Get a new ETag at v1/orders/{OrderId.toString orderId} and try again." |},
                        statusCode = (int) HttpStatusCode.PreconditionFailed
                    )
            | Error error -> return invalidOp $"Error '{error}' is unexpected." |> raise
        }

    let private handle orderIdString (context: HttpContext) =
        let checkStatus = checkStatus (context.GetService<FindStatus>())
        let cancelOrder = cancelOrder (context.GetService<SetStatusToCanceled>())

        apiOperation {
            let! orderId = validateOrderId orderIdString
            let! eTag = tryGetETag context
            let! _ = checkStatus orderId
            return! cancelOrder orderId eTag
        }
        |> ApiOperation.toHttpHandler

    let private getHandler orderIdString : HttpHandler =
        fun next context ->
            task {
                let handler = handle orderIdString context
                return! handler next context
            }

    let list = [ DELETE [ routef "/%s" getHandler ] ]
