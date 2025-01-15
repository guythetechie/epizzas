using Azure.Monitor.OpenTelemetry.AspNetCore;
using LanguageExt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using static LanguageExt.Prelude;

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

public static class ActivityModule
{
    [return: NotNullIfNotNull(nameof(activity))]
    public static Activity? AddSerializedTag(this Activity? activity, string key, object? value) =>
        activity?.SetTag(key,
                         JsonSerializer.SerializeToNode(value,
                                                        value?.GetType() ?? typeof(object),
                                                        JsonSerializerOptions.Web));
}