module api.integration.tests.Program

open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open OpenTelemetry.Metrics
open OpenTelemetry.Trace
open System
open System.Diagnostics
open System.Net.Http
open FSharpPlus
open common
open Microsoft.Extensions.Configuration

let private getActivitySource (provider: IServiceProvider) =
    new ActivitySource("api.integration.tests")

let private configureActivitySource (builder: IHostApplicationBuilder) =
    ServiceCollection.tryAddSingleton builder.Services getActivitySource

let private configureMetrics (builder: MeterProviderBuilder) =
    builder |> tap OpenTelemetry.configureMetrics |> ignore
//|> Http.configureOpenTelemetryMetrics

let private configureTracing (builder: TracerProviderBuilder) =
    builder
    |> tap OpenTelemetry.configureTracing
    |> tap Cosmos.configureOpenTelemetryTracing
    //|> tap Http.configureOpenTelemetryTracing
    |> OpenTelemetry.setAlwaysOnSampler

let private configureTelemetry (builder: IHostApplicationBuilder) =
    builder
    |> tap configureActivitySource
    |> _.Services.AddOpenTelemetry()
    |> tap (OpenTelemetry.setDestination builder.Configuration)
    |> _.WithMetrics(configureMetrics)
    |> _.WithTracing(configureTracing)
    |> ignore

let private getLogger provider =
    let factory = ServiceProvider.getServiceOrThrow<ILoggerFactory> provider
    factory.CreateLogger("api.integration.tests")

let private configureLogger (builder: IHostApplicationBuilder) =
    ServiceCollection.tryAddSingleton builder.Services getLogger

let private configureBuilder builder =
    configureTelemetry builder
    configureLogger builder
    Cosmos.configureOrdersContainerBuilder builder
    Api.configureHttpClientBuilder builder

let private startHost (host: IHost) =
    async {
        let! cancellationToken = Async.CancellationToken
        do! host.StartAsync(cancellationToken) |> Async.AwaitTask
    }

let private getApplicationLifetime (host: IHost) =
    ServiceProvider.getServiceOrThrow<IHostApplicationLifetime> host.Services

let private run (host: IHost) =
    async {
        let applicationLifetime = getApplicationLifetime host
        let activitySource = ServiceProvider.getServiceOrThrow<ActivitySource> host.Services
        let logger = ServiceProvider.getServiceOrThrow<ILogger> host.Services

        let ordersContainer =
            ServiceProvider.getServiceOrThrow<OrdersContainer> host.Services

        let getApiClient () =
            let configuration = ServiceProvider.getServiceOrThrow<IConfiguration> host.Services
            let connectionName = Api.getConnectionName configuration
            let factory = ServiceProvider.getServiceOrThrow<IHttpClientFactory> host.Services
            factory.CreateClient(connectionName)

        try
            try
                use _ = Activity.fromSource "test" activitySource

                logger.LogInformation "Starting host..."
                do! startHost host

                logger.LogInformation "Running order tests..."
                do! Orders.test (ordersContainer, getApiClient)
            with error ->
                logger.LogCritical(error, "Application failed.")
                Environment.ExitCode <- -1
                raise error
        finally
            applicationLifetime.StopApplication()
    }

[<EntryPoint>]
let main args =
    let builder = Host.CreateApplicationBuilder(args)
    configureBuilder builder

    let host = builder.Build()
    let applicationLifetime = getApplicationLifetime host
    let cancellationToken = applicationLifetime.ApplicationStopping
    let computation = run host
    Async.RunSynchronously(computation, cancellationToken = cancellationToken)

    0
