using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

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

        TryGetConfigurationStoreUrl(configuration)
            .Iter(uri => ConfigureAppConfiguration(builder, uri, tokenCredential));
    }

    private static Option<Uri> TryGetConfigurationStoreUrl(IConfiguration configuration) =>
        Try.lift(() => GetConfigurationStoreUrl(configuration))
           .Run()
           .ToOption();

    private static Uri GetConfigurationStoreUrl(IConfiguration configuration)
    {
        var url = configuration.GetValue("AZURE_APP_CONFIGURATION_STORE_URL");
        return new Uri(url);
    }

    private static void ConfigureAppConfiguration(IConfigurationBuilder builder, Uri configurationStoreUri, TokenCredential tokenCredential) =>
        builder.AddAzureAppConfiguration(options => options.Connect(configurationStoreUri, tokenCredential)
                                                           .Select(KeyFilter.Any, labelFilter: "CTOF-IMPORTS"),
                                         optional: true);

    public static void ConfigureAzureEnvironment(IServiceCollection services)
    {
        services.TryAddSingleton(GetAzureEnvironment);
    }

    private static AzureEnvironment GetAzureEnvironment(IServiceProvider provider)
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

        var options = new DefaultAzureCredentialOptions
        {
            AuthorityHost = azureEnvironment.AuthenticationEndpoint,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true
        };

        return new DefaultAzureCredential(options);
    }
}