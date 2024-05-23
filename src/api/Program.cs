using Asp.Versioning.ApiExplorer;
using common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
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
        ConfigureEndpoints(services);
        ConfigureVersioning(services);
        ConfigureSwagger(services);
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

    private static void ConfigureEndpoints(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
    }

    private static void ConfigureVersioning(IServiceCollection services)
    {
        services.AddApiVersioning(options => options.ReportApiVersions = true)
                .AddApiExplorer(options => options.GroupNameFormat = "'v'VVV");
    }

    private static void ConfigureSwagger(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen(options => options.OperationFilter<SwaggerOperationFilter>());
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
        ConfigureEndpoints(application);
    }

    private static void ConfigureEndpoints(WebApplication application)
    {
        var builder = application.NewVersionedApi();

        v1.Endpoints.Map(builder);
    }

    private static void ConfigureSwagger(WebApplication application)
    {
        application.UseSwagger();

        if (application.Environment.IsDevelopment())
        {
            application.UseSwaggerUI(
                options =>
                {
                    var descriptions = application.DescribeApiVersions();

                    // build a swagger endpoint for each discovered API version
                    foreach (var description in descriptions)
                    {
                        var url = $"/swagger/{description.GroupName}/swagger.json";
                        var name = description.GroupName.ToUpperInvariant();
                        options.SwaggerEndpoint(url, name);
                    }
                });
        }
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

file sealed class SwaggerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiDescription = context.ApiDescription;

        operation.Deprecated |= apiDescription.IsDeprecated();

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1752#issue-663991077
        foreach (var responseType in context.ApiDescription.SupportedResponseTypes)
        {
            // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/b7cf75e7905050305b115dd96640ddd6e74c7ac9/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGenerator.cs#L383-L387
            var responseKey = responseType.IsDefaultResponse ? "default" : responseType.StatusCode.ToString();
            var response = operation.Responses[responseKey];

            foreach (var contentType in response.Content.Keys)
            {
                if (!responseType.ApiResponseFormats.Any(x => x.MediaType == contentType))
                {
                    response.Content.Remove(contentType);
                }
            }
        }

        if (operation.Parameters == null)
        {
            return;
        }

        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/412
        // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/413
        foreach (var parameter in operation.Parameters)
        {
            var description = apiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);

            parameter.Description ??= description.ModelMetadata?.Description;

            if (parameter.Schema.Default == null &&
                 description.DefaultValue != null &&
                 description.DefaultValue is not DBNull &&
                 description.ModelMetadata is ModelMetadata modelMetadata)
            {
                // REF: https://github.com/Microsoft/aspnet-api-versioning/issues/429#issuecomment-605402330
                var json = JsonSerializer.Serialize(description.DefaultValue, modelMetadata.ModelType);
                parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
            }

            parameter.Required |= description.IsRequired;
        }
    }
}

file sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider) : IConfigureOptions<SwaggerGenOptions>
{
    /// <inheritdoc />
    public void Configure(SwaggerGenOptions options)
    {
        // add a swagger document for each discovered API version
        // note: you might choose to skip or document deprecated API versions differently
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
        }
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description)
    {
        var text = new StringBuilder("An example application with OpenAPI, Swashbuckle, and API versioning.");
        var info = new OpenApiInfo()
        {
            Title = "EPizzas API",
            Description = "EPizzas API",
            Version = description.ApiVersion.ToString(),
        };

        if (description.IsDeprecated)
        {
            text.Append(" This API version has been deprecated.");
        }

        if (description.SunsetPolicy is { } policy)
        {
            if (policy.Date is { } when)
            {
                text.Append(" The API will be sunset on ")
                    .Append(when.Date.ToShortDateString())
                    .Append('.');
            }

            if (policy.HasLinks)
            {
                text.AppendLine();

                var rendered = false;

                for (var i = 0; i < policy.Links.Count; i++)
                {
                    var link = policy.Links[i];

                    if (link.Type == "text/html")
                    {
                        if (!rendered)
                        {
                            text.Append("<h4>Links</h4><ul>");
                            rendered = true;
                        }

                        text.Append("<li><a href=\"");
                        text.Append(link.LinkTarget.OriginalString);
                        text.Append("\">");
                        text.Append(
                            StringSegment.IsNullOrEmpty(link.Title)
                            ? link.LinkTarget.OriginalString
                            : link.Title.ToString());
                        text.Append("</a></li>");
                    }
                }

                if (rendered)
                {
                    text.Append("</ul>");
                }
            }
        }

        text.Append("<h4>Additional Information</h4>");
        info.Description = text.ToString();

        return info;
    }
}