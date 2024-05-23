using common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using System;
using System.Threading.Tasks;

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
        ConfigureServices(builder);
    }

    private static void ConfigureServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        OpenTelemetryServices.Configure(services, "api");
        AzureServices.ConfigureAzureEnvironment(services);
        ConfigureAuthentication(services);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    private static void ConfigureAuthentication(IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var azureEnvironment = serviceProvider.GetRequiredService<AzureEnvironment>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(bearerOptions => { },
                                            identityOptions =>
                                            {
                                                identityOptions.Instance = azureEnvironment.AuthenticationEndpoint.ToString();
                                                identityOptions.ClientId = configuration.GetValue("AZURE_CLIENT_ID");
                                                identityOptions.TenantId = configuration.GetValue("AZURE_TENANT_ID");
                                            },
                                            subscribeToJwtBearerMiddlewareDiagnosticsEvents: true);

        services.AddAuthorization();

    }

    private static void ConfigureWebApplication(WebApplication application)
    {
        if (application.Environment.IsDevelopment())
        {
            application.UseSwagger();
            application.UseSwaggerUI();
        }

        application.UseHttpsRedirection();

        application.UseAuthentication();
        application.UseAuthorization();
    }

    //    public static void Main(string[] args)
    //    {
    //        var builder = WebApplication.CreateBuilder(args);

    //        // Add services to the container.
    //        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    //            .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    //        builder.Services.AddAuthorization();

    //        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    //        builder.Services.AddEndpointsApiExplorer();
    //        builder.Services.AddSwaggerGen();

    //        var app = builder.Build();

    //        // Configure the HTTP request pipeline.
    //        if (app.Environment.IsDevelopment())
    //        {
    //            app.UseSwagger();
    //            app.UseSwaggerUI();
    //        }

    //        app.UseHttpsRedirection();

    //        app.UseAuthentication();
    //        app.UseAuthorization();

    //        var scopeRequiredByApi = app.Configuration["AzureAd:Scopes"] ?? "";
    //        var summaries = new[]
    //        {
    //    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    //};

    //        app.MapGet("/weatherforecast", (HttpContext httpContext) =>
    //        {
    //            httpContext.VerifyUserHasAnyAcceptedScope(scopeRequiredByApi);

    //            var forecast = Enumerable.Range(1, 5).Select(index =>
    //                new WeatherForecast
    //                (
    //                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
    //                    Random.Shared.Next(-20, 55),
    //                    summaries[Random.Shared.Next(summaries.Length)]
    //                ))
    //                .ToArray();
    //            return forecast;
    //        })
    //        .WithName("GetWeatherForecast")
    //        .WithOpenApi()
    //        .RequireAuthorization();

    //        app.Run();
    //    }
}