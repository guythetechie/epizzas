using common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;

namespace portal;

internal delegate HttpClient GetApiClient();

internal static class ApiModule
{
    private const string apiClientKey = "api client";

    public static void ConfigureGetApiClient(IHostApplicationBuilder builder)
    {
        HttpModule.AddResilience(builder);
        HttpModule.AddServiceDiscovery(builder);

        builder.Services.AddHttpClient(apiClientKey, client =>
        {
            var apiConnectionName = builder.Configuration.GetValueOrThrow("API_CONNECTION_NAME");

            client.BaseAddress = new($"https+http://{apiConnectionName}");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.TryAddSingleton(GetGetApiClient);
    }

    private static GetApiClient GetGetApiClient(IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        return () => factory.CreateClient(apiClientKey);
    }
}
