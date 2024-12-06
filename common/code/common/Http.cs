using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace common;

public static class HttpModule
{
    public static void ConfigureTelemetry(OpenTelemetryBuilder builder) =>
        builder.WithTracing(tracing => tracing.AddHttpClientInstrumentation())
               .WithMetrics(metrics => metrics.AddHttpClientInstrumentation());

    public static void AddResilience(IHostApplicationBuilder builder) =>
        builder.Services.ConfigureHttpClientDefaults(builder => builder.AddStandardResilienceHandler());

    public static void AddServiceDiscovery(IHostApplicationBuilder builder) =>
        builder.Services
               .AddServiceDiscovery()
               .ConfigureHttpClientDefaults(builder => builder.AddServiceDiscovery());
}