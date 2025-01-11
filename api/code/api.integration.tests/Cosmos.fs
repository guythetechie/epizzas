namespace api.integration.tests

open Aspire.Microsoft.Azure.Cosmos
open Azure.Core
open Microsoft.Azure.Cosmos
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System
open System.Text.Json
open FSharpPlus
open FSharp.Control
open common

type OrdersContainer =
    | OrdersContainer of Container

    static member toContainer(OrdersContainer container) = container

[<RequireQualifiedAccess>]
module private Cosmos =
    let private deleteOrder (OrdersContainer container) json =
        let cosmosId, partitionKey, eTag =
            monad {
                let! id = Cosmos.getId json
                let! partitionKey = PartitionKey.fromOrderJson json
                let! eTag = Cosmos.getETag json
                return id, partitionKey, eTag
            }
            |> JsonResult.throwIfFail

        async {
            match! Cosmos.deleteRecord container partitionKey cosmosId eTag with
            | Ok() -> return ()
            | Error error -> return failwith $"Failed to delete order. Error is '{error}'."
        }


    let emptyContainer ordersContainer =
        async {
            let query =
                CosmosQueryOptions.fromQueryString "SELECT c.id, c.orderId, c._etag FROM c"

            let container = OrdersContainer.toContainer ordersContainer

            do!
                Cosmos.getQueryResults container query
                |> AsyncSeq.iterAsyncParallel (deleteOrder ordersContainer)
        }

    let private configureCosmosSettings configuration provider (settings: MicrosoftAzureCosmosSettings) =
        let getConfigurationValue = Configuration.getValue configuration

        let tryParseUri value =
            match Uri.TryCreate(value, UriKind.Absolute) with
            | true, uri -> Some uri
            | _ -> None

        getConfigurationValue "COSMOS_ACCOUNT_ENDPOINT"
        |> bind tryParseUri
        |> iter (fun endpoint ->
            settings.AccountEndpoint <- endpoint
            let tokenCredential = ServiceProvider.getServiceOrThrow<TokenCredential> provider
            settings.Credential <- tokenCredential)

        getConfigurationValue "COSMOS_CONNECTION_STRING"
        |> Option.iter (fun connectionString -> settings.ConnectionString <- connectionString)

    let private configureClientOptions (options: CosmosClientOptions) =
        options.EnableContentResponseOnWrite <- false
        options.UseSystemTextJsonSerializerWithOptions <- JsonSerializerOptions.Web

        options.CosmosClientTelemetryOptions <-
            let options = CosmosClientTelemetryOptions()
            options.DisableDistributedTracing <- false
            options

    let private configureClient (builder: IHostApplicationBuilder) =
        Azure.configureTokenCredential builder

        let configuration = builder.Configuration

        let connectionName =
            Configuration.getValue configuration "COSMOS_CONNECTION_NAME"
            |> Option.defaultValue String.Empty

        let configureCosmosSettings =
            configureCosmosSettings configuration (builder.Services.BuildServiceProvider())

        builder.AddAzureCosmosClient(connectionName, configureCosmosSettings, configureClientOptions)

    let private getDatabase (provider: IServiceProvider) =
        let client = provider.GetRequiredService<CosmosClient>()
        let configuration = provider.GetRequiredService<IConfiguration>()

        let databaseName =
            Configuration.getValueOrThrow configuration "COSMOS_DATABASE_NAME"

        client.GetDatabase(databaseName)

    let private configureDatabase builder =
        configureClient builder

        ServiceCollection.tryAddSingleton builder.Services getDatabase

    let private getOrdersContainer (provider: IServiceProvider) =
        let database = provider.GetRequiredService<Database>()
        let configuration = provider.GetRequiredService<IConfiguration>()

        let containerName =
            Configuration.getValueOrThrow configuration "COSMOS_ORDERS_CONTAINER_NAME"

        database.GetContainer(containerName) |> OrdersContainer

    let configureOrdersContainerBuilder builder =
        configureDatabase builder

        ServiceCollection.tryAddSingleton builder.Services getOrdersContainer
