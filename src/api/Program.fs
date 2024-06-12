open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Azure.Cosmos
open Aspire.Microsoft.Azure.Cosmos
open FSharpPlus
open common
open Giraffe.EndpointRouting

[<RequireQualifiedAccess>]
module private Application =
    let private endpoints = V1.Endpoints.list

    let private configure application =
        HealthCheck.configureApplication application
        application.UseHttpsRedirection() |> ignore
        application.UseRouting().UseGiraffe(endpoints) |> ignore
        application.MapGroup("")

    let createFromBuilder (builder: WebApplicationBuilder) = builder.Build() |> tap configure

[<RequireQualifiedAccess>]
module private ApplicationBuilder =
    let private configureCosmos (builder: WebApplicationBuilder) =
        let configureCosmosClient () =
            let configureSettings (settings: MicrosoftAzureCosmosSettings) =
                let setCosmosConnectionString connectionString =
                    settings.ConnectionString <- connectionString

                let setCosmosAccountEndpoint accountEndpoint =
                    settings.AccountEndpoint <- new Uri(accountEndpoint)

                let tryGetConfigurationValue key =
                    Configuration.tryGetValue builder.Configuration key

                tryGetConfigurationValue "COSMOS_CONNECTION_STRING"
                |> Option.iter setCosmosConnectionString

                tryGetConfigurationValue "COSMOS_ACCOUNT_ENDPOINT"
                |> Option.iter setCosmosAccountEndpoint

            let configureOptions (options: CosmosClientOptions) =
                options.AllowBulkExecution <- true
                options.EnableContentResponseOnWrite <- false

            builder.AddAzureCosmosClient("cosmos", configureSettings, configureOptions)

        let configureCosmosDatabase () =
            let getCosmosDatabase (provider: IServiceProvider) =
                let client = provider.GetRequiredService<CosmosClient>()

                let databaseName =
                    let configuration = provider.GetRequiredService<IConfiguration>()
                    Configuration.getValue configuration "COSMOS_DATABASE_NAME"

                client.GetDatabase databaseName

            builder.Services.AddSingleton<Database>(getCosmosDatabase) |> ignore

        configureCosmosClient ()
        configureCosmosDatabase ()

    let private configureServices (builder: WebApplicationBuilder) =
        let services = builder.Services
        OpenTelemetry.configureServices services "api"
        HealthCheck.configureServices services
        V1.Services.configure services

    type private Dummy = class end

    let private configureConfiguration (builder: WebApplicationBuilder) =
        builder.Configuration.AddUserSecrets(typeof<Dummy>.Assembly, optional = true)
        |> ignore

    let private configure (builder: WebApplicationBuilder) =
        configureConfiguration builder
        configureServices builder
        configureCosmos builder

    let create (arguments: string array) =
        WebApplication.CreateBuilder(arguments) |> tap configure

[<EntryPoint>]
let main arguments =
    ApplicationBuilder.create arguments |> Application.createFromBuilder |> _.Run()

    0 // Exit code
