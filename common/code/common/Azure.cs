using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

namespace common;

public static class AzureModule
{
    public static void ConfigureTokenCredential(IHostApplicationBuilder builder) =>
        builder.Services.TryAddSingleton(GetTokenCredential);

    private static TokenCredential GetTokenCredential(IServiceProvider provider)
    {
        var authorityHost = GetAuthorityHost(provider);

        var options = new DefaultAzureCredentialOptions
        {
            AuthorityHost = authorityHost,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true
        };

        return new DefaultAzureCredential(options);
    }

    private static Uri GetAuthorityHost(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>()
                .GetValue("AZURE_CLOUD_ENVIRONMENT")
                .Map(environment => environment switch
                {
                    "AzureGlobalCloud" or nameof(AzureAuthorityHosts.AzurePublicCloud) =>
                        AzureAuthorityHosts.AzurePublicCloud,
                    "AzureUSGovernment" or nameof(AzureAuthorityHosts.AzureGovernment) =>
                        AzureAuthorityHosts.AzureGovernment,
                    nameof(AzureAuthorityHosts.AzureChina) => AzureAuthorityHosts.AzureChina,
                    _ => throw new InvalidOperationException($"""
'{environment}' is not a valid cloud environment. Valid values are {nameof(AzureAuthorityHosts.AzurePublicCloud)}, {nameof(AzureAuthorityHosts.AzureChina)}, {nameof(AzureAuthorityHosts.AzureGovernment)}.
""")
                })
                .IfNone(() => AzureAuthorityHosts.AzurePublicCloud);
}