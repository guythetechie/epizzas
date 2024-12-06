using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace common;

public static class HostingModule
{
    public static async ValueTask RunHost(IHost host, Func<IServiceProvider, CancellationToken, ValueTask> f)
    {
        await StartHost(host);

        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var cancellationToken = applicationLifetime.ApplicationStopping;
        var activitySource = host.Services.GetService<ActivitySource>();

        try
        {
            using var _ = activitySource?.StartActivity(activitySource.Name);

            await f(host.Services, cancellationToken);
        }
        catch (Exception exception)
        {
            var logger =
                host.Services.GetService<ILogger>()
                ?? host.Services.GetService<ILoggerFactory>()?.CreateLogger(activitySource?.Name ?? "Program");
            logger?.LogCritical(exception, "Application failed.");

            Environment.ExitCode = -1;
            throw;
        }
        finally
        {
            applicationLifetime.StopApplication();
        }
    }

    public static async ValueTask StartHost(IHost host)
    {
        var applicationLifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var cancellationToken = applicationLifetime.ApplicationStopping;
        await host.StartAsync(cancellationToken);
    }
}