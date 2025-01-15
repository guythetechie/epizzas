using common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace api.integration.tests;

internal delegate ValueTask<HttpResponseMessage> FindOrder(OrderId orderId, CancellationToken cancellationToken);
internal delegate ValueTask<HttpResponseMessage> CreateOrder(Order order, CancellationToken cancellationToken);
internal delegate ValueTask<HttpResponseMessage> CancelOrder(OrderId orderId, ETag ETag, CancellationToken cancellationToken);
internal delegate ValueTask<HttpResponseMessage> ListOrders(CancellationToken cancellationToken);
internal delegate HttpClient GetApiClient();

internal static class ApiModule
{
    public static void ConfigureListOrders(IHostApplicationBuilder builder)
    {
        ConfigureGetApiClient(builder);

        builder.Services.TryAddSingleton(GetListOrders);
    }

    private static ListOrders GetListOrders(IServiceProvider provider)
    {
        var getClient = provider.GetRequiredService<GetApiClient>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async cancellationToken =>
        {
            using var activity = activitySource.StartActivity(nameof(ListOrders));

            var response = await GetApiResponse(getClient,
                                                "/v1/orders",
                                                async (uri, client) => await client.GetAsync(uri, cancellationToken));

            activity?.SetTag("statusCode", response.StatusCode);

            return response;
        };
    }

    public static void ConfigureCancelOrder(IHostApplicationBuilder builder)
    {
        ConfigureGetApiClient(builder);

        builder.Services.TryAddSingleton(GetCancelOrder);
    }

    private static CancelOrder GetCancelOrder(IServiceProvider provider)
    {
        var getClient = provider.GetRequiredService<GetApiClient>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (orderId, eTag, cancellationToken) =>
        {
            using var activity = activitySource.StartActivity(nameof(CancelOrder))
                                              ?.SetTag("orderId", orderId)
                                              ?.SetTag("eTag", eTag);

            var response = await GetApiResponse(getClient,
                                                $"/v1/orders/{orderId}",
                                                async (uri, client) =>
                                                {
                                                    using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
                                                    request.Headers.Add("If-Match", eTag.ToString());
                                                    return await client.SendAsync(request, cancellationToken);
                                                });

            activity?.SetTag("statusCode", response.StatusCode);

            return response;
        };
    }


    public static void ConfigureCreateOrder(IHostApplicationBuilder builder)
    {
        ConfigureGetApiClient(builder);

        builder.Services.TryAddSingleton(GetCreateOrder);
    }

    private static CreateOrder GetCreateOrder(IServiceProvider provider)
    {
        var getClient = provider.GetRequiredService<GetApiClient>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (order, cancellationToken) =>
        {
            using var activity = activitySource.StartActivity(nameof(CreateOrder))
                                              ?.SetTag("order", Order.Serialize(order));

            var response = await GetApiResponse(
                getClient,
                "/v1/orders",
                async (uri, client) =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                    {
                        Content = JsonContent.Create(Order.Serialize(order))
                    };

                    request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);

                    return await client.SendAsync(request, cancellationToken);
                });

            activity?.SetTag("statusCode", response.StatusCode);

            return response;
        };
    }

    public static void ConfigureFindOrder(IHostApplicationBuilder builder)
    {
        ConfigureGetApiClient(builder);

        builder.Services.TryAddSingleton(GetFindOrder);
    }

    private static FindOrder GetFindOrder(IServiceProvider provider)
    {
        var getClient = provider.GetRequiredService<GetApiClient>();
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return async (orderId, cancellationToken) =>
        {
            using var activity = activitySource.StartActivity(nameof(FindOrder))
                                              ?.SetTag("orderId", orderId);

            var response = await GetApiResponse(getClient,
                                                $"/v1/orders/{orderId}",
                                                async (uri, client) => await client.GetAsync(uri, cancellationToken));

            activity?.SetTag("statusCode", response.StatusCode);

            return response;
        };
    }

    private static async ValueTask<HttpResponseMessage> GetApiResponse(
        GetApiClient getClient,
        string relativeUrl,
        Func<Uri, HttpClient, ValueTask<HttpResponseMessage>> f)
    {
        using var client = getClient();
        var uri = new Uri(relativeUrl, UriKind.Relative);
        return await f(uri, client);
    }

    public static void ConfigureGetApiClient(IHostApplicationBuilder builder)
    {
        //HttpModule.AddResilience(builder);
        HttpModule.AddServiceDiscovery(builder);

        builder.Services.AddHttpClient(ApiClientKey, client =>
        {
            var apiConnectionName = builder.Configuration.GetValueOrThrow("API_CONNECTION_NAME");

            client.BaseAddress = new($"https+http://{apiConnectionName}");
            client.Timeout = TimeSpan.FromSeconds(300);
        });

        builder.Services.TryAddSingleton(GetGetApiClient);
    }

    private static GetApiClient GetGetApiClient(IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return () => factory.CreateClient(ApiClientKey);
    }

    private static string ApiClientKey { get; } = nameof(ApiClientKey);
}
