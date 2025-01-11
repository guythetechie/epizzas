namespace portal;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using portal.Components;
using System.Threading.Tasks;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureBuilder(builder);

        var application = builder.Build();
        ConfigureApplication(application);

        await application.RunAsync();
    }

    private static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        ConfigureTelemetry(builder);
        ApiModule.ConfigureGetApiClientResponse(builder);
        ConfigureBlazor(builder);
        Components.Orders.CommonModule.ConfigureListOrders(builder);
    }

    private static void ConfigureTelemetry(WebApplicationBuilder builder)
    {
        var telemetryBuilder = builder.Services.AddOpenTelemetry();

        common.OpenTelemetry.setDestination(builder.Configuration, telemetryBuilder);

        telemetryBuilder.WithTracing(tracing => tracing.AddAspNetCoreInstrumentation()
                                                       .AddHttpClientInstrumentation());

        telemetryBuilder.WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation()
                                                       .AddHttpClientInstrumentation());
    }

    private static void ConfigureBlazor(WebApplicationBuilder builder)
    {
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddFluentUIComponents();
    }

    private static void ConfigureApplication(WebApplication application)
    {
        if (!application.Environment.IsDevelopment())
        {
            application.UseExceptionHandler("/Error", createScopeForErrors: true);
            application.UseHsts();
        }

        application.UseHttpsRedirection();

        application.MapStaticAssets();

        application.UseAntiforgery();

        application
            .MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
    }
}