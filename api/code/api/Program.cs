using System.Threading.Tasks;
using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace api;

internal static class Program
{
    public static async Task Main(string[] arguments)
    {
        var builder = WebApplication.CreateBuilder(arguments);
        ConfigureBuilder(builder);

        var application = builder.Build();
        ConfigureWebApplication(application);

        await application.RunAsync();
    }

    private static void ConfigureBuilder(IHostApplicationBuilder builder)
    {
        ConfigureConfiguration(builder);
        ConfigureTelemetry(builder);
        HealthCheckModule.ConfigureBuilder(builder);
        OrdersModule.ConfigureBuilder(builder);
    }

    private static void ConfigureTelemetry(IHostApplicationBuilder builder)
    {
        var telemetryBuilder = builder.Services.AddOpenTelemetry();

        OpenTelemetryModule.ConfigureDestination(telemetryBuilder, builder.Configuration);
        OpenTelemetryModule.SetAlwaysOnSampler(telemetryBuilder);
        OpenTelemetryModule.ConfigureAspNetCoreInstrumentation(telemetryBuilder);
        common.CosmosModule.ConfigureTelemetry(telemetryBuilder);
    }

    private static void ConfigureConfiguration(IHostApplicationBuilder builder) =>
        builder.Configuration.AddUserSecrets(typeof(Program).Assembly);

    //private static void ConfigureAuthentication(IServiceCollection services)
    //{
    //    var serviceProvider = services.BuildServiceProvider();

    //    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    //    var azureEnvironment = serviceProvider.GetRequiredService<AzureEnvironment>();
    //    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    //            .AddMicrosoftIdentityWebApi(bearerOptions => { },
    //                                        identityOptions =>
    //                                        {
    //                                            identityOptions.Instance = azureEnvironment.AuthenticationEndpoint.ToString();
    //                                            identityOptions.ClientId = configuration.GetValueOrThrow("AZURE_CLIENT_ID");
    //                                            identityOptions.TenantId = configuration.GetValueOrThrow("AZURE_TENANT_ID");
    //                                        },
    //                                        subscribeToJwtBearerMiddlewareDiagnosticsEvents: true);

    //    services.AddAuthorization();
    //}

    private static void ConfigureWebApplication(WebApplication application)
    {
        application.UseHttpsRedirection();
        HealthCheckModule.ConfigureWebApplication(application);
        OrdersModule.ConfigureEndpoints(application);
    }
}
