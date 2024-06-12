[<RequireQualifiedAccess>]
module common.OpenTelemetry

open Azure.Monitor.OpenTelemetry.AspNetCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.DependencyInjection.Extensions
open Microsoft.Extensions.Logging
open OpenTelemetry.Logs
open OpenTelemetry.Metrics
open OpenTelemetry.Trace
open System.Diagnostics

let configureServices (services: IServiceCollection) activitySourceName =
    services.TryAddSingleton(new ActivitySource(activitySourceName))

    let _ =
        services
            .AddOpenTelemetry()
            .WithMetrics(fun metrics ->
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("*")
                |> ignore)
            .WithTracing(fun tracing ->
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("*")
                    .SetSampler<AlwaysOnSampler>()
                |> ignore)

    let configuration = services.BuildServiceProvider().GetService<IConfiguration>()

    Configuration.tryGetValue configuration "OTEL_EXPORTER_OTLP_ENDPOINT"
    |> Option.iter (fun _ ->
        services
            .AddLogging(fun builder -> builder.AddOpenTelemetry() |> ignore)
            .Configure<OpenTelemetryLoggerOptions>(fun (options: OpenTelemetryLoggerOptions) ->
                options.AddOtlpExporter() |> ignore)
            .ConfigureOpenTelemetryMeterProvider(fun metrics -> metrics.AddOtlpExporter() |> ignore)
            .ConfigureOpenTelemetryTracerProvider(fun tracing -> tracing.AddOtlpExporter() |> ignore)
        |> ignore)

    Configuration.tryGetValue configuration "APPLICATIONINSIGHTS_CONNECTION_STRING"
    |> Option.iter (fun _ -> services.AddOpenTelemetry().UseAzureMonitor() |> ignore)
