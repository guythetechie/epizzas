module api.Program

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open OpenTelemetry.Metrics
open OpenTelemetry.Trace
open FSharpPlus
open common

let private configureMetrics (builder: MeterProviderBuilder) = OpenTelemetry.configureMetrics builder

let private configureTracing (builder: TracerProviderBuilder) =
    OpenTelemetry.configureTracing builder
    Cosmos.configureOpenTelemetryTracing builder
    OpenTelemetry.setAlwaysOnSampler builder

let private configureTelemetry (builder: IHostApplicationBuilder) =
    builder.Services.AddOpenTelemetry()
    |> tap (OpenTelemetry.setDestination builder.Configuration)
    |> _.WithMetrics(configureMetrics)
    |> _.WithTracing(configureTracing)
    |> ignore

let private configureBuilder builder =
    configureTelemetry builder
    HealthCheck.configureBuilder builder
    Oxpecker.configureBuilder builder

let private configureApplication (application: WebApplication) =
    let _ = application.UseHttpsRedirection()
    Oxpecker.configureApplication application

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureBuilder builder

    let application = builder.Build()
    let _ = configureApplication application

    application.Run()

    0 // Exit code
