using Azure.Core;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.ResourceManager;
using Flurl;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace common;

public sealed record AzureEnvironment(Uri AuthenticationEndpoint, Uri ManagementEndpoint, Uri GraphEndpoint)
{
    public static AzureEnvironment Public { get; } = new(AzureAuthorityHosts.AzurePublicCloud, ArmEnvironment.AzurePublicCloud.Endpoint, new("https://graph.microsoft.com"));

    public static AzureEnvironment USGovernment { get; } = new(AzureAuthorityHosts.AzureGovernment, ArmEnvironment.AzureGovernment.Endpoint, new("https://graph.microsoft.us"));
}

public static class AzureModule
{
    public static void ConfigureAppConfiguration(IHostApplicationBuilder builder) =>
        builder.Configuration
               .GetValue("AZURE_APP_CONFIGURATION_STORE_URL")
               .Iter(url =>
               {
                   var uri = new Uri(url);

                   ConfigureTokenCredential(builder);

                   var services = builder.Services;
                   var serviceProvider = services.BuildServiceProvider();
                   var tokenCredential = serviceProvider.GetRequiredService<TokenCredential>();

                   ConfigureAppConfiguration(builder.Configuration, tokenCredential, uri);

                   services.TryAddSingleton(provider => GetConfigurationClient(provider, uri));
               });

    private static void ConfigureAppConfiguration(IConfigurationManager configuration, TokenCredential tokenCredential, Uri endpoint) =>
        configuration.AddAzureAppConfiguration(options =>
        {
            options.Connect(endpoint, tokenCredential)
                   .Select(KeyFilter.Any, LabelFilter.Null);

            configuration.GetValue("AZURE_APP_CONFIGURATION_STORE_LABEL")
                         .Iter(label => options.Select(KeyFilter.Any, labelFilter: label));
        }, optional: false);

    public static void ConfigureTokenCredential(IHostApplicationBuilder builder)
    {
        ConfigureAzureEnvironment(builder);

        builder.Services.TryAddSingleton(GetTokenCredential);
    }

    private static TokenCredential GetTokenCredential(IServiceProvider provider)
    {
        var azureEnvironment = provider.GetRequiredService<AzureEnvironment>();

        var options = new DefaultAzureCredentialOptions
        {
            AuthorityHost = azureEnvironment.AuthenticationEndpoint,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true
        };

        return new DefaultAzureCredential(options);
    }

    public static void ConfigureAzureEnvironment(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(GetAzureEnvironment);
    }

    private static AzureEnvironment GetAzureEnvironment(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();

        return configuration.GetValue("AZURE_CLOUD_ENVIRONMENT").ValueUnsafe() switch
        {
            null => AzureEnvironment.USGovernment,
            "AzureGlobalCloud" or nameof(ArmEnvironment.AzurePublicCloud) => AzureEnvironment.Public,
            "AzureUSGovernment" or nameof(ArmEnvironment.AzureGovernment) => AzureEnvironment.USGovernment,
            _ => throw new InvalidOperationException($"AZURE_CLOUD_ENVIRONMENT is invalid. Valid values are {nameof(ArmEnvironment.AzurePublicCloud)}, {nameof(ArmEnvironment.AzureChina)}, {nameof(ArmEnvironment.AzureGovernment)}, {nameof(ArmEnvironment.AzureGermany)}")
        };
    }

    private static ConfigurationClient GetConfigurationClient(IServiceProvider provider, Uri endpoint)
    {
        var tokenCredential = provider.GetRequiredService<TokenCredential>();

        return new ConfigurationClient(endpoint, tokenCredential);
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