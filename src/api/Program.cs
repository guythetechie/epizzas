using common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        ConfigureConfiguration(builder);
        ConfigureServices(builder);
        ConfigureCosmos(builder);
    }

    private static void ConfigureConfiguration(IHostApplicationBuilder builder)
    {
        builder.Configuration.AddUserSecrets(typeof(Program).Assembly);
    }

    private static void ConfigureServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        OpenTelemetryServices.Configure(services, "api");
        HealthCheckModule.ConfigureServices(services);
        AzureServices.ConfigureAzureEnvironment(services);
        //ConfigureAuthentication(services);
        //ConfigureEndpoints(services);
        ConfigureVersioning(services);

        v1.Services.Configure(services);
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

    private static void ConfigureVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
        });
    }

    private static void ConfigureCosmos(IHostApplicationBuilder builder)
    {
        builder.AddAzureCosmosClient("cosmos",
                                     settings =>
                                     {
                                         var configuration = builder.Configuration;
                                         configuration.TryGetValue("COSMOS_CONNECTION_STRING")
                                                      .Iter(connectionString => settings.ConnectionString = connectionString);

                                         configuration.TryGetValue("COSMOS_ACCOUNT_ENDPOINT")
                                                      .Iter(accountEndpoint => settings.AccountEndpoint = new(accountEndpoint));
                                     },
                                     options =>
                                     {
                                         options.AllowBulkExecution = true;
                                         options.EnableContentResponseOnWrite = false;
                                         options.Serializer = CosmosModule.Serializer;
                                     });

        builder.Services.TryAddSingleton(GetCosmosDatabase);
    }

    private static Database GetCosmosDatabase(IServiceProvider provider)
    {
        var client = provider.GetRequiredService<CosmosClient>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        var databaseName = configuration.GetValue("COSMOS_DATABASE_NAME");
        return client.GetDatabase(databaseName);
    }

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

        v1.Endpoints.Map(builder);
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