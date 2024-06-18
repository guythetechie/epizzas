namespace V1.Orders.Common

open Microsoft.Azure.Cosmos
open System
open System.Text.Json.Nodes
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open FSharp.Control
open common

type OrdersCosmosContainer =
    | OrdersCosmosContainer of Container

    static member toContainer(OrdersCosmosContainer container) = container

// Disables warning for implicit conversion in serializeOrder function. F# limitations require that the directive appear before the module instead of being closer to the function definition.
#nowarn "3391"

[<RequireQualifiedAccess>]
module Services =
    let private getOrdersCosmosContainer (provider: IServiceProvider) =
        let database = provider.GetRequiredService<Database>()

        let containerName =
            let configuration = provider.GetRequiredService<IConfiguration>()
            Configuration.getValue configuration "COSMOS_ORDERS_CONTAINER_NAME"

        database.GetContainer containerName |> OrdersCosmosContainer

    let configure (services: IServiceCollection) =
        services.AddSingleton<OrdersCosmosContainer>(getOrdersCosmosContainer)

[<RequireQualifiedAccess>]
module Cosmos =
    let private deserializeTopping toppingJson =
        let kind = JsonObject.getStringProperty toppingJson "kind" |> ToppingKind.fromString

        let amount =
            JsonObject.getStringProperty toppingJson "amount" |> ToppingAmount.fromString

        (kind, amount)

    let private deserializePizza pizzaJson =
        let size = JsonObject.getStringProperty pizzaJson "size" |> PizzaSize.fromString

        let toppings =
            JsonObject.getJsonArrayProperty pizzaJson "toppings"
            |> Seq.map JsonNode.toJsonObject
            |> Seq.map deserializeTopping
            |> Map.ofSeq

        { Pizza.Size = size
          Toppings = toppings }

    let private statusPropertyName = "status"

    let private getStatus orderJson =
        JsonObject.getStringProperty orderJson statusPropertyName
        |> OrderStatus.fromString

    let private deserializeOrder orderJson =
        let id = JsonObject.getStringProperty orderJson "id" |> OrderId.fromString

        let status = getStatus orderJson

        let pizzas =
            JsonObject.getJsonArrayProperty orderJson "pizzas"
            |> Seq.map JsonNode.toJsonObject
            |> Seq.map deserializePizza
            |> Seq.toList

        { Order.Id = id
          Status = status
          Pizzas = pizzas }

    let findOrder ordersContainer orderId =
        let container = OrdersCosmosContainer.toContainer ordersContainer
        let cosmosId = OrderId.toString orderId |> CosmosId.fromString
        let partitionKey = OrderId.toString orderId |> PartitionKey
        Cosmos.tryReadRecord container cosmosId partitionKey deserializeOrder

    let findOrderStatus ordersContainer orderId =
        let container = OrdersCosmosContainer.toContainer ordersContainer

        let query =
            { CosmosQueryOptions.Query =
                let queryDefinition =
                    QueryDefinition($"SELECT c.{statusPropertyName} FROM c WHERE c.id = @id")

                queryDefinition.WithParameter("@id", OrderId.toString orderId)
              PartitionKey = OrderId.toString orderId |> PartitionKey |> Some
              ContinuationToken = None }

        async {
            match! Cosmos.getQueryResults container query |> TaskSeq.tryHead |> Async.AwaitTask with
            | Some json ->
                return
                    JsonObject.tryGetStringProperty json statusPropertyName
                    |> Result.map OrderStatus.fromString
                    |> Result.toOption
            | None -> return None
        }

    let tryUpdateOrderStatus ordersContainer orderId eTag status =
        let container = OrdersCosmosContainer.toContainer ordersContainer
        let cosmosId = OrderId.toString orderId |> CosmosId.fromString
        let partitionKey = OrderId.toString orderId |> PartitionKey

        let operations =
            [ PatchOperation.Set($"/{statusPropertyName}", OrderStatus.toString status) ]

        Cosmos.patchRecord container cosmosId partitionKey eTag operations

    let listOrders ordersContainer =
        let container = OrdersCosmosContainer.toContainer ordersContainer

        let query =
            { CosmosQueryOptions.Query =
                QueryDefinition($"SELECT c.id, c.pizzas, c.{statusPropertyName}, c._etag FROM c")
              PartitionKey = None
              ContinuationToken = None }

        Cosmos.getQueryResults container query
        |> TaskSeq.map (fun json -> deserializeOrder json, AzureETag.fromCosmosJsonObject json)

    let private serializeTopping (kind, amount) =
        [ ("kind", ToppingKind.toString kind |> JsonNode.op_Implicit)
          ("amount", ToppingAmount.toString amount) ]
        |> JsonObject.fromSeq

    let private serializePizza (pizza: Pizza) =
        [ ("size", PizzaSize.toString pizza.Size |> JsonNode.op_Implicit)
          ("toppings", pizza.Toppings |> Map.toSeq |> Seq.map serializeTopping |> JsonArray.fromSeq :> JsonNode) ]
        |> JsonObject.fromSeq

    let private serializeOrder (order: Order) =
        [ ("id", OrderId.toString order.Id |> JsonNode.op_Implicit)
          ("status", OrderStatus.toString order.Status)
          ("pizzas", order.Pizzas |> Seq.map serializePizza |> JsonArray.fromSeq :> JsonNode) ]
        |> JsonObject.fromSeq

    let createOrder ordersContainer order =
        let container = OrdersCosmosContainer.toContainer ordersContainer
        let json = serializeOrder order
        let partitionKey = OrderId.toString order.Id |> PartitionKey

        Cosmos.createRecord container json partitionKey
