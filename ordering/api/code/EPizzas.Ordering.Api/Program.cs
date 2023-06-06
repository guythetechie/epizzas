using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace EPizzas.Ordering.Api;


public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureBuilder(builder);

        var application = builder.Build();
        ConfigureApplication(application);

        await application.RunAsync();
    }

    private static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        ConfigureServices(builder.Services);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetry()
                .AddAuthentication();

        ConfigureJson(services);
        ConfigureVersioning(services);

        V1.Services.Configure(services);
    }

    private static void ConfigureJson(IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });
    }

    private static void ConfigureVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options => options.ReportApiVersions = true);
    }

    private static void ConfigureApplication(WebApplication application)
    {
        if (application.Environment.IsDevelopment() is false)
        {
            application.UseExceptionHandler(ConfigureExceptionHandler);
        }

        application.UseStatusCodePages();

        ConfigureRoutes(application);
    }

    private static void ConfigureExceptionHandler(IApplicationBuilder builder)
    {
        var error = new
        {
            code = nameof(ErrorCode.InternalServerError),
            message = "An error has occurred."
        };

        builder.Run(async context => await TypedResults.Json(error, statusCode: StatusCodes.Status500InternalServerError)
                                                       .ExecuteAsync(context));
    }

    public static void ConfigureRoutes<T>(T builder) where T : IApplicationBuilder, IEndpointRouteBuilder
    {
        var versionedRouteBuilder = builder.NewVersionedApi();

        V1.Endpoints.Map(versionedRouteBuilder);
    }
}