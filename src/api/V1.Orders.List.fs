namespace V1.Orders.List

open System
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Azure
open Giraffe
open Giraffe.EndpointRouting
open FSharp.Control
open common
open V1.Orders.Common

type private ListOrders = unit -> TaskSeq<Order * ETag>

[<RequireQualifiedAccess>]
module Services =
    let private configureListOrders =
        Func<IServiceProvider, ListOrders>(fun provider ->
            fun () ->
                let container = provider.GetRequiredService<OrdersCosmosContainer>()
                Cosmos.listOrders container)

    let configure (services: IServiceCollection) =
        services.AddSingleton<ListOrders>(configureListOrders) |> ignore

[<RequireQualifiedAccess>]
module Endpoints =
    let private serializeOrder (order: Order) (eTag: ETag) =
        {| id = OrderId.toString order.Id
           pizzas =
            order.Pizzas
            |> Seq.map (fun pizza ->
                {| size = PizzaSize.toString pizza.Size
                   toppings =
                    pizza.Toppings
                    |> Map.toSeq
                    |> Seq.map (fun (kind, amount) ->
                        {| kind = ToppingKind.toString kind
                           amount = ToppingAmount.toString amount |}) |})
           eTag = eTag.ToString() |}

    let private listOrders (f: ListOrders) =
        async {
            let! orders = f () |> TaskSeq.toListAsync |> Async.AwaitTask
            return Results.Ok(orders |> Seq.map (fun (order, eTag) -> serializeOrder order eTag))
        }

    let private handle (context: HttpContext) =
        let listOrders = listOrders (context.GetService<ListOrders>())

        apiOperation { return! listOrders } |> ApiOperation.toHttpHandler

    let private handler: HttpHandler = fun next context -> (handle context) next context

    let list = [ GET [ route "/" handler ] ]
