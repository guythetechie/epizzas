using common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace api;

#pragma warning disable CA1515 // Consider making public types internal
#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable
public class Program
#pragma warning restore CA1052 // Static holder types should be Static or NotInheritable
#pragma warning restore CA1515 // Consider making public types internal
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

        OpenTelemetryModule.Configure(builder, "api");
        HealthCheckModule.ConfigureBuilder(builder);
        //ConfigureAuthentication(builder.Services);
        ConfigureVersioning(builder.Services);

        v1.CommonModule.ConfigureApplicationBuilder(builder);
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

    private static void ConfigureVersioning(IServiceCollection services) =>
        services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
        });

    private static void ConfigureWebApplication(WebApplication application)
    {
        application.UseHttpsRedirection();
        HealthCheckModule.ConfigureWebApplication(application);
        //application.UseAuthentication();
        //application.UseAuthorization();
        ConfigureEndpoints(application);
    }

    private static void ConfigureEndpoints(WebApplication application)
    {
        var builder = application.NewVersionedApi();

        v1.CommonModule.ConfigureEndpoints(builder);
    }
}