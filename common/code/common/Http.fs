[<RequireQualifiedAccess>]
module common.Http

open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open global.OpenTelemetry
open OpenTelemetry.Trace
open OpenTelemetry.Metrics

let configureOpenTelemetryMetrics (metrics: MeterProviderBuilder) =
    metrics.AddHttpClientInstrumentation() |> ignore

let configureOpenTelemetryTracing (tracing: TracerProviderBuilder) =
    tracing.AddHttpClientInstrumentation() |> ignore

let addResilience (builder: IHostApplicationBuilder) =
    builder.Services.ConfigureHttpClientDefaults(fun builder -> builder.AddStandardResilienceHandler() |> ignore)
    |> ignore

let addServiceDiscovery (builder: IHostApplicationBuilder) =
    builder.Services
        .AddServiceDiscovery()
        .ConfigureHttpClientDefaults(fun builder -> builder.AddServiceDiscovery() |> ignore)
    |> ignore
