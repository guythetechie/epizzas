using common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace api.integration.tests;

internal static class HttpModule
{
    public static string ApiClientKey { get; } = nameof(ApiClientKey);

    public static void ConfigureHttpClient(IHostApplicationBuilder builder)
    {
        builder.Services.AddServiceDiscovery()
                        .ConfigureHttpClientDefaults(http =>
                        {
                            http.AddStandardResilienceHandler();
                            http.AddServiceDiscovery();
                        })
                        .AddHttpClient(ApiClientKey, client =>
                        {
                            var apiConnectionName = builder.Configuration.GetValueOrThrow("ASPIRE_API_CONNECTION_NAME");

                            client.BaseAddress = new($"https+http://{apiConnectionName}");
                            client.Timeout = TimeSpan.FromMinutes(3);
                        });
    }
}
