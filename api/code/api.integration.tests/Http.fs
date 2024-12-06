[<RequireQualifiedAccess>]
module api.integration.tests.Http

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open OpenTelemetry.Metrics
open OpenTelemetry.Trace
open System
open System.Net.Http
open common

let configureOpenTelemetryMetrics (metrics: MeterProviderBuilder) =
    metrics.AddHttpClientInstrumentation() |> ignore

let configureOpenTelemetryTracing (tracing: TracerProviderBuilder) =
    tracing.AddHttpClientInstrumentation() |> ignore

let private configureHttpClientBuilder (builder: IHttpClientBuilder) =
    builder.AddStandardResilienceHandler() |> ignore
    builder.AddServiceDiscovery() |> ignore

let apiIdentifier = "api"

let private configureApiHttpClient configuration (client: HttpClient) =
    let connectionName =
        Configuration.getValueOrThrow configuration "API_CONNECTION_NAME"

    client.BaseAddress <- Uri($"https+http://{connectionName}")

let configureBuilder (builder: IHostApplicationBuilder) =
    builder.Services
        .AddServiceDiscovery()
        .ConfigureHttpClientDefaults(configureHttpClientBuilder)
        .AddHttpClient(apiIdentifier, configureApiHttpClient builder.Configuration)
    |> ignore
