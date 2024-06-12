namespace V1.Orders.GetById

open Azure
open FSharpPlus
open Microsoft.AspNetCore.Http
open System
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.EndpointRouting
open common
open V1.Orders.Common

type private FindOrder = OrderId -> Async<Option<Order * ETag>>

[<RequireQualifiedAccess>]
module Services =
    let private configureFindOrder =
        Func<IServiceProvider, FindOrder>(fun provider ->
            fun orderId ->
                let container = provider.GetRequiredService<OrdersCosmosContainer>()

                async {
                    match! Cosmos.findOrder container orderId with
                    | Some cosmosRecord -> return Some(cosmosRecord.Record, cosmosRecord.ETag)
                    | None -> return None
                })

    let configure (services: IServiceCollection) =
        services.AddSingleton<FindOrder>(configureFindOrder) |> ignore

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

    let private findOrder (f: FindOrder) orderId =
        monad {
            let! orderOption = f orderId

            return
                orderOption
                |> Option.toResultWith (
                    Results.NotFound
                        {| code = ApiErrorCode.toString ApiErrorCode.ResourceNotFound
                           message = $"Order with ID '{OrderId.toString orderId}' was not found." |}
                )
        }

    let private getSuccessfulResult (order: Order) (eTag: ETag) =
        Results.Ok(
            {| id = OrderId.toString order.Id
               eTag = eTag.ToString() |}
        )

    let private handle orderIdString (context: HttpContext) =
        let findOrder = findOrder (context.GetService<FindOrder>())

        apiOperation {
            let! orderId = validateOrderId orderIdString
            let! (order, eTag) = findOrder orderId
            return getSuccessfulResult order eTag
        }
        |> ApiOperation.toHttpHandler

    let private getHandler orderIdString : HttpHandler =
        fun next context ->
            task {
                let handler = handle orderIdString context
                return! handler next context
            }

    let list = [ GET [ routef "/%s" getHandler ] ]
