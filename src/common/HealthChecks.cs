using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace common;

public static class HealthCheckModule
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddHealthChecks()
        // Add a default liveness check to ensure app is responsive
                .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
    }

    public static void ConfigureWebApplication(this WebApplication application)
    {
        // Adding health checks endpoints to applications in non-development 
        // environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before 
        // enabling these endpoints in non-development environments.
        if (application.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to 
            // accept traffic after starting
            application.MapHealthChecks("/health");

            // Only health checks tagged with the "live" tag must pass for 
            // app to be considered alive
            application.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains("live")
            });
        }
    }
}
