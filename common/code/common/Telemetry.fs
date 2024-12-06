namespace common

open Azure.Monitor.OpenTelemetry.AspNetCore
open FSharpPlus
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
        Option.ofObj activity |> iter _.Dispose()

[<RequireQualifiedAccess>]
module OpenTelemetry =
    let setDestination configuration (builder: IOpenTelemetryBuilder) =
        Configuration.getValue configuration "APPLICATION_INSIGHTS_CONNECTION_STRING"
        |> iter (fun _ -> match builder with
                          | :? OpenTelemetryBuilder as builder -> builder.UseAzureMonitor() |> ignore
                          | _ -> ())
        

        Configuration.getValue configuration "OTEL_EXPORTER_OTLP_ENDPOINT"
        |> iter (fun _ -> match builder with
                          | :? OpenTelemetryBuilder as builder -> builder.UseOtlpExporter() |> ignore
                          | _ -> ())

    let configureMetrics (builder: MeterProviderBuilder) =
        builder.AddAspNetCoreInstrumentation()
               .AddRuntimeInstrumentation() |> ignore

    let setAlwaysOnSampler (builder: TracerProviderBuilder) =
        builder.SetSampler(AlwaysOnSampler()) |> ignore
    
    let configureTracing (builder: TracerProviderBuilder) =
        builder.AddAspNetCoreInstrumentation() |> ignore