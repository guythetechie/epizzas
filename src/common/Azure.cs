using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Flurl;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace common;

public sealed record AzureEnvironment(Uri AuthenticationEndpoint, Uri ManagementEndpoint, Uri GraphEndpoint)
{
    public static AzureEnvironment Public { get; } = new(AzureAuthorityHosts.AzurePublicCloud, ArmEnvironment.AzurePublicCloud.Endpoint, new("https://graph.microsoft.com"));

    public static AzureEnvironment USGovernment { get; } = new(AzureAuthorityHosts.AzureGovernment, ArmEnvironment.AzureGovernment.Endpoint, new("https://graph.microsoft.us"));
}

public static class AzureServices
{
    public static void ConfigureAppConfiguration(IConfigurationBuilder builder, TokenCredential tokenCredential)
    {
        var configuration = builder.Build();

        configuration.TryGetValue("AZURE_APP_CONFIGURATION_STORE_URL")
                     .Iter(url => ConfigureAppConfiguration(builder, new Uri(url), tokenCredential));
    }

    private static void ConfigureAppConfiguration(IConfigurationBuilder builder, Uri configurationStoreUri, TokenCredential tokenCredential) =>
        builder.AddAzureAppConfiguration(options => options.Connect(configurationStoreUri, tokenCredential)
                                                           .Select(KeyFilter.Any, labelFilter: "CTOF-IMPORTS"),
                                         optional: true);

    public static void ConfigureAzureEnvironment(IServiceCollection services)
    {
        services.TryAddSingleton(GetAzureEnvironment);
    }

    public static AzureEnvironment GetAzureEnvironment(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();

        return configuration.TryGetValue("AZURE_CLOUD_ENVIRONMENT").ValueUnsafe() switch
        {
            null => AzureEnvironment.USGovernment,
            "AzureGlobalCloud" or nameof(ArmEnvironment.AzurePublicCloud) => AzureEnvironment.Public,
            "AzureUSGovernment" or nameof(ArmEnvironment.AzureGovernment) => AzureEnvironment.USGovernment,
            _ => throw new InvalidOperationException($"AZURE_CLOUD_ENVIRONMENT is invalid. Valid values are {nameof(ArmEnvironment.AzurePublicCloud)}, {nameof(ArmEnvironment.AzureChina)}, {nameof(ArmEnvironment.AzureGovernment)}, {nameof(ArmEnvironment.AzureGermany)}")
        };
    }

    public static void ConfigureTokenCredential(IServiceCollection services)
    {
        ConfigureAzureEnvironment(services);

        services.TryAddSingleton(GetTokenCredential);
    }

    private static TokenCredential GetTokenCredential(IServiceProvider provider)
    {
        var azureEnvironment = provider.GetRequiredService<AzureEnvironment>();

        return GetTokenCredential(azureEnvironment);
    }

    public static TokenCredential GetTokenCredential(AzureEnvironment azureEnvironment)
    {
        var options = new DefaultAzureCredentialOptions
        {
            AuthorityHost = azureEnvironment.AuthenticationEndpoint,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true
        };

        return new DefaultAzureCredential(options);
    }
}

public static class TokenCredentialExtensions
{
    public static async ValueTask<string> GetAccessTokenString(this TokenCredential tokenCredential, Uri uri, CancellationToken cancellationToken)
    {
        var scope = uri.RemovePath()
                       .RemoveQuery()
                       .AppendPathSegment(".default")
                       .ToString();

        return await tokenCredential.GetAccessTokenString([scope], cancellationToken);
    }

    public static async ValueTask<string> GetAccessTokenString(this TokenCredential tokenCredential, string[] scopes, CancellationToken cancellationToken)
    {
        var context = new TokenRequestContext(scopes);

        var token = await tokenCredential.GetTokenAsync(context, cancellationToken);

        return token.Token;
    }
}