[<RequireQualifiedAccess>]
module Azure

open Azure.Core
open Azure.Identity
open Microsoft.Extensions.Configuration
open FSharpPlus
open common
open Microsoft.Extensions.Hosting

let private getAuthorityHost provider =
    ServiceProvider.getService<IConfiguration> provider
    |> bind (fun configuration -> Configuration.getValue configuration "AZURE_CLOUD_ENVIRONMENT")
    |> (function
    | Some "AzureUSGovernment"
    | Some "AzureGovernment" -> AzureAuthorityHosts.AzureGovernment
    | Some "AzureChina" -> AzureAuthorityHosts.AzureChina
    | Some "AzureGlobalCloud"
    | Some "AzureCloud"
    | Some "AzurePublicCloud" -> AzureAuthorityHosts.AzurePublicCloud
    | Some value ->
        failwith
            $"'{value}' is not valid for 'AZURE_CLOUD_ENVIRONMENT'. Allowed values are 'AzureGovernment', 'AzureChina', and 'AzurePublicCloud'."
    | None -> AzureAuthorityHosts.AzurePublicCloud)

let private getTokenCredential provider : TokenCredential =
    let authorityHost = getAuthorityHost provider

    let options = DefaultAzureCredentialOptions()
    options.AuthorityHost <- authorityHost
    options.ExcludeVisualStudioCredential <- true
    options.ExcludeVisualStudioCodeCredential <- true

    DefaultAzureCredential(options)

let configureTokenCredential (builder: IHostApplicationBuilder) =
    ServiceCollection.tryAddSingleton builder.Services getTokenCredential
