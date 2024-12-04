[<RequireQualifiedAccess>]
module Oxpecker

open Microsoft.AspNetCore.Builder
open Microsoft.Azure.Cosmos
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System
open System.Diagnostics
open FSharp.Control
open Oxpecker
open common
open common.Serialization
open api

type private Env(application: WebApplication) =
    let provider = application.Services

    let getActivitySource () =
        provider.GetRequiredService<ActivitySource>()

    let getConfiguration () =
        provider.GetRequiredService<IConfiguration>()

    let getOrdersContainer () =
        provider.GetRequiredKeyedService<Container>(Cosmos.ordersContainerIdentifier)

    let getTimeProvider () =
        provider.GetRequiredService<TimeProvider>()

    interface IFindCosmosOrder with
        member this.FindCosmosOrder orderId =
            async {
                let activitySource = getActivitySource ()

                use _ =
                    Activity.fromSource "cosmos.find_order" activitySource
                    |> Activity.setTag "order_id" (OrderId.toString orderId)

                let container = getOrdersContainer ()

                let query =
                    CosmosQueryOptions.fromQueryString
                        """
                        SELECT c.orderId, c.status, c.pizzas, c._etag
                        FROM c
                        WHERE c.orderId = @orderId
                        """
                    |> CosmosQueryOptions.setQueryParameter "@orderId" (OrderId.toString orderId)

                let! results = Cosmos.getQueryResults container query |> AsyncSeq.toListAsync

                match results with
                | [] -> return None
                | [ json ] ->
                    let order = Order.deserialize json |> JsonResult.throwIfFail
                    let eTag = Cosmos.getETag json |> JsonResult.throwIfFail
                    return Some(order, eTag)
                | _ -> return failwith $"Found more than one order with id {OrderId.toString orderId}."
            }

    interface IListCosmosOrders with
        member this.ListCosmosOrders() =
            asyncSeq {
                let activitySource = getActivitySource ()

                use _ = Activity.fromSource "cosmos.list_orders" activitySource

                let container = getOrdersContainer ()

                let query =
                    CosmosQueryOptions.fromQueryString
                        """
                        SELECT c.orderId, c.status, c.pizzas, c._etag
                        FROM c
                        """

                yield!
                    Cosmos.getQueryResults container query
                    |> AsyncSeq.map (fun json ->
                        let order = Order.deserialize json |> JsonResult.throwIfFail
                        let eTag = Cosmos.getETag json |> JsonResult.throwIfFail
                        order, eTag)
            }

    interface ICancelCosmosOrder with
        member this.CancelCosmosOrder orderId eTag =
            async {
                let activitySource = getActivitySource ()

                use _ =
                    Activity.fromSource "cosmos.cancel_order" activitySource
                    |> Activity.setTag "order_id" (OrderId.toString orderId)

                let container = getOrdersContainer ()

                let query =
                    CosmosQueryOptions.fromQueryString
                        """
                            SELECT c.id
                            FROM c
                            WHERE c.orderId = @orderId
                            """
                    |> CosmosQueryOptions.setQueryParameter "@orderId" (OrderId.toString orderId)

                let! results = Cosmos.getQueryResults container query |> AsyncSeq.toListAsync

                match results with
                | [] -> return Error CosmosError.NotFound
                | [ json ] ->
                    let id = Cosmos.getId json |> JsonResult.throwIfFail
                    let partitionKey = PartitionKey(OrderId.toString orderId)

                    let status =
                        OrderStatus.Cancelled
                            {| By = "system"
                               Date = getTimeProvider().GetUtcNow() |}

                    let patchOperation =
                        Seq.singleton (PatchOperation.Set("/status", OrderStatus.serialize status))

                    return! Cosmos.patchRecord container partitionKey id eTag patchOperation
                | _ -> return failwith $"Found more than one order with id {OrderId.toString orderId}."
            }

    static member private getTimeProvider(provider: IServiceProvider) = TimeProvider.System

    static member private configureTimeProvider(builder: IHostApplicationBuilder) =
        ServiceCollection.tryAddSingleton builder.Services Env.getTimeProvider

    static member configureBuilder builder =
        Env.configureTimeProvider builder
        Cosmos.configureOrdersContainer builder

let configureBuilder builder =
    Env.configureBuilder builder
    builder.Services.AddOxpecker() |> ignore

let configureApplication application =
    let env = Env(application)
    let endpoints = subRoute "/v1" [ Orders.getEndpoint env ]

    let _ = application.UseRouting()
    application.UseOxpecker(endpoints)
