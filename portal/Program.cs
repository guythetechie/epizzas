namespace portal;

using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using portal.Components;
using System.Threading.Tasks;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureBuilder(builder);

        using var application = builder.Build();
        ConfigureApplication(application);

        await application.RunAsync();
    }

    private static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        ConfigureTelemetry(builder);
        ConfigureBlazor(builder);
        ConfigureOrders(builder);
    }

    private static void ConfigureTelemetry(WebApplicationBuilder builder)
    {
        OpenTelemetryModule.ConfigureActivitySource(builder, nameof(portal));

        var telemetryBuilder = builder.Services.AddOpenTelemetry();
        OpenTelemetryModule.ConfigureDestination(telemetryBuilder, builder.Configuration);
        OpenTelemetryModule.ConfigureAspNetCoreInstrumentation(telemetryBuilder);
        OpenTelemetryModule.SetAlwaysOnSampler(telemetryBuilder);
    }

    private static void ConfigureBlazor(WebApplicationBuilder builder)
    {
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddFluentUIComponents();
    }

    private static void ConfigureOrders(WebApplicationBuilder builder)
    {
        Components.Orders.CommonModule.ConfigureListOrders(builder);
        Components.Orders.CommonModule.ConfigureCreateOrder(builder);
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