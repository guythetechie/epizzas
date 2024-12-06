using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using common;
using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using static LanguageExt.Prelude;

namespace api.integration.tests;

internal delegate HttpClient GetApiClient();

internal static class ApiModule
{
    public static Eff<GetApiClient, HttpResponseMessage> ListOrders() =>
        GetClientResponse(
                "/v1/orders",
                async (client, uri, cancellationToken) => await client.GetAsync(uri, cancellationToken)
            )
            .As();

    private static Eff<GetApiClient, HttpResponseMessage> GetClientResponse(
        string relativeUriString,
        Func<HttpClient, Uri, CancellationToken, ValueTask<HttpResponseMessage>> f
    ) =>
        from getApiClient in runtime<GetApiClient>()
        from client in use(getApiClient.Invoke)
        let uri = new Uri(relativeUriString, UriKind.Relative)
        from cancellationToken in cancelToken
        from response in liftIO(async () => await f(client, uri, cancellationToken))
        select response;

    public static Eff<GetApiClient, HttpResponseMessage> GetOrder(string orderId) =>
        GetClientResponse(
                $"/v1/orders/{orderId}",
                async (client, uri, cancellationToken) => await client.GetAsync(uri, cancellationToken)
            )
            .As();

    public static Eff<GetApiClient, HttpResponseMessage> CreateOrder(JsonNode json) =>
        GetClientResponse(
                "/v1/orders",
                async (client, uri, cancellationToken) =>
                {
                    using var content = JsonContent.Create(json, options: JsonSerializerOptions.Web);
                    return await client.PostAsync(uri, content, cancellationToken);
                }
            )
            .As();

    public static Eff<GetApiClient, HttpResponseMessage> CancelOrder(string orderId) =>
        GetClientResponse(
                $"/v1/orders/{orderId}",
                async (client, uri, cancellationToken) => await client.DeleteAsync(uri, cancellationToken)
            )
            .As();

    public static void ConfigureGetApiClient(IHostApplicationBuilder builder)
    {
        HttpModule.AddResilience(builder);
        HttpModule.AddServiceDiscovery(builder);

        builder.Services.AddHttpClient(
            ApiClientKey,
            client =>
            {
                var apiConnectionName = builder.Configuration.GetValueOrThrow("API_CONNECTION_NAME");

                client.BaseAddress = new($"https+http://{apiConnectionName}");
                client.Timeout = TimeSpan.FromMinutes(3);
            }
        );

        builder.Services.TryAddSingleton(GetGetApiClient);
    }

    private static GetApiClient GetGetApiClient(IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return () => factory.CreateClient(ApiClientKey);
    }

    private static string ApiClientKey { get; } = nameof(ApiClientKey);
}
