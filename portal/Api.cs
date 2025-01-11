using common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace portal;

internal delegate ValueTask<HttpResponseMessage> GetApiClientResponse(
    string relativeUrl,
    Func<HttpClient, Uri, ValueTask<HttpResponseMessage>> f);

internal static class ApiModule
{
    private const string apiClientKey = "api client";

    public static void ConfigureGetApiClientResponse(IHostApplicationBuilder builder)
    {
        Http.addResilience(builder);
        Http.addServiceDiscovery(builder);

        builder.Services.AddHttpClient(apiClientKey, client =>
        {
            var apiConnectionName = Configuration.getValueOrThrow(builder.Configuration, "API_CONNECTION_NAME");

            client.BaseAddress = new($"https+http://{apiConnectionName}");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.TryAddSingleton(GetGetApiClientResponse);
    }

    private static GetApiClientResponse GetGetApiClientResponse(IServiceProvider serviceProvider)
    {
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        return async (relativeUrl, f) =>
        {
            using var client = factory.CreateClient(apiClientKey);
            var uri = new Uri(relativeUrl, UriKind.Relative);
            return await f(client, uri);
        };
    }
}
