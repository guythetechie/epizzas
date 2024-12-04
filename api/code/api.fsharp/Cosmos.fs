[<RequireQualifiedAccess>]
module Cosmos

open Aspire.Microsoft.Azure.Cosmos
open Azure.Core
open Microsoft.Azure.Cosmos
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System
open System.Text.Json
open FSharpPlus
open common

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

    AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
    
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

let ordersContainerIdentifier = "orders container"

let private getOrdersContainer (provider: IServiceProvider) =
    let database = provider.GetRequiredService<Database>()
    let configuration = provider.GetRequiredService<IConfiguration>()

    let containerName =
        Configuration.getValueOrThrow configuration "COSMOS_ORDERS_CONTAINER_NAME"

    database.GetContainer(containerName)

let configureOrdersContainer builder =
    configureDatabase builder

    ServiceCollection.tryAddKeyedSingleton ordersContainerIdentifier builder.Services getOrdersContainer
