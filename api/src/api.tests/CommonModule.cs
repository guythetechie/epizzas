
#pragma warning disable IDE0005 // Using directive is unnecessary.
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using common;
using DotNext.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
#pragma warning restore IDE0005 // Using directive is unnecessary.
using System.Threading;
using System.Threading.Tasks;

namespace api.tests;

internal static class CommonModule
{
    public static AsyncLazy<string> GetApiProjectName { get; } = new(async cancellationToken =>
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.aspire>(cancellationToken);
        return builder.Configuration.GetValue("ASPIRE_API_PROJECT_NAME").IfNone("api");
    });

    public static async ValueTask<DistributedApplication> GetDistributedApplication(CancellationToken cancellationToken)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.aspire>(cancellationToken);
        var application = await builder.BuildAsync(cancellationToken);
        await application.StartAsync(cancellationToken);

        //var apiProjectName = await GetApiProjectName.WithCancellation(cancellationToken);
        //var notificationService = application.Services.GetRequiredService<ResourceNotificationService>();
        //await notificationService.WaitForResourceAsync(apiProjectName, KnownResourceStates.Running, cancellationToken)
        //                         .WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);

        return application;
    }
}