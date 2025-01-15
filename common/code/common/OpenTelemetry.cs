using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace common;

public static class OpenTelemetryModule
{
    public static void ConfigureActivitySource(IHostApplicationBuilder builder, string activitySourceName)
    {
        builder.Services.TryAddSingleton(provider => new ActivitySource(activitySourceName));
    }

    public static void ConfigureDestination(OpenTelemetryBuilder builder, IConfiguration configuration)
    {
        configuration.GetValue("APPLICATIONINSIGHTS_CONNECTION_STRING")
                     .Iter(_ => builder.UseAzureMonitor());

        configuration.GetValue("OTEL_EXPORTER_OTLP_ENDPOINT")
                     .Iter(_ => builder.UseOtlpExporter());
    }

    public static void ConfigureAspNetCoreInstrumentation(OpenTelemetryBuilder builder) =>
        builder.WithMetrics(builder => builder.AddAspNetCoreInstrumentation())
               .WithTracing(builder => builder.AddAspNetCoreInstrumentation());

    public static void SetAlwaysOnSampler(OpenTelemetryBuilder builder) =>
        builder.WithTracing(builder => builder.SetSampler(new AlwaysOnSampler()));
}