namespace common

open Azure.Monitor.OpenTelemetry.AspNetCore
open FSharpPlus
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System.Diagnostics
open OpenTelemetry
open OpenTelemetry.Metrics
open OpenTelemetry.Trace

[<RequireQualifiedAccess>]
module Activity =
    let fromSource (name: string) (activitySource: ActivitySource) = activitySource.StartActivity(name)

    let setTag key value (activity: Activity | null): Activity | null =
        match activity with
        | Null -> null
        | NonNull activity -> activity.SetTag(key, value)

    let dispose (activity: Activity | null) =
        match activity with
        | Null -> ()
        | NonNull activity -> activity.Dispose()

[<RequireQualifiedAccess>]
module OpenTelemetry =
    let private setDestination configuration (builder: IOpenTelemetryBuilder) =
        Configuration.getValue configuration "APPLICATION_INSIGHTS_CONNECTION_STRING"
        |> iter (fun _ -> match builder with
                          | :? OpenTelemetryBuilder as builder -> builder.UseAzureMonitor() |> ignore
                          | _ -> ())
        

        Configuration.getValue configuration "OTEL_EXPORTER_OTLP_ENDPOINT"
        |> iter (fun _ -> match builder with
                          | :? OpenTelemetryBuilder as builder -> builder.UseOtlpExporter() |> ignore
                          | _ -> ())

        builder

    let private configureMetrics (builder: IOpenTelemetryBuilder) =
        builder.WithMetrics(fun metrics -> metrics.AddAspNetCoreInstrumentation()
                                                  .AddRuntimeInstrumentation() |> ignore)
    
    let private configureTracing (builder: IOpenTelemetryBuilder) =
        builder.WithTracing(fun tracing -> tracing.SetSampler(new AlwaysOnSampler())
                                                  .AddAspNetCoreInstrumentation() |> ignore)

    let configureBuilder (builder: IHostApplicationBuilder) =
        builder.Services.AddOpenTelemetry()
        |> setDestination builder.Configuration
        |> configureMetrics
        |> configureTracing
        |> ignore