[<RequireQualifiedAccess>]
module api.integration.tests.Api

open Microsoft.Extensions.DependencyInjection
open System
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open System.Text.Json.Nodes
open common

let private getClientResponse (getClient: unit -> HttpClient) relativeUriString f =
    async {
        use client = getClient ()
        let uri = Uri(relativeUriString, UriKind.Relative)
        let! cancellationToken = Async.CancellationToken
        return! f client uri cancellationToken |> Async.AwaitTask
    }

let private sendGetRequest getClient relativeUriString =
    getClientResponse getClient relativeUriString (fun client uri cancellationToken ->
        client.GetAsync(uri, cancellationToken))

let listOrders getClient = sendGetRequest getClient "/v1/orders"

let getOrder getClient orderId =
    sendGetRequest getClient $"/v1/orders/%s{orderId}"

let createOrder getClient (orderJson: JsonNode | null) =
    getClientResponse getClient $"/v1/orders" (fun client uri cancellationToken ->
        use content = JsonContent.Create(orderJson, options = JsonSerializerOptions.Web)
        client.PostAsync(uri, content, cancellationToken))

let cancelOrder getClient orderId =
    getClientResponse getClient $"/v1/orders/%s{orderId}" (fun client uri cancellationToken ->
        client.DeleteAsync(uri, cancellationToken))

let getConnectionName configuration =
    Configuration.getValueOrThrow configuration "API_CONNECTION_NAME"

let private configureApiHttpClient configuration (client: HttpClient) =
    let connectionName = getConnectionName configuration

    client.BaseAddress <- Uri($"https+http://{connectionName}")

let configureHttpClientBuilder builder =
    Http.addResilience builder
    Http.addServiceDiscovery builder

    let connectionName = getConnectionName builder.Configuration

    builder.Services.AddHttpClient(connectionName, configureApiHttpClient builder.Configuration)
    |> ignore
