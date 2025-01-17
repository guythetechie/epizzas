using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;

namespace api;

internal delegate DateTimeOffset GetCurrentTime();

internal static class CommonModule
{
    public static void ConfigureGetCurrentTime(IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(GetGetCurrentTime);
    }

    private static GetCurrentTime GetGetCurrentTime(IServiceProvider provider)
    {
        var activitySource = provider.GetRequiredService<ActivitySource>();

        return () =>
        {
            var activity = activitySource.StartActivity(nameof(GetCurrentTime));

            var time = DateTimeOffset.UtcNow;

            activity?.AddTag(nameof(time), time);

            return time;
        };
    }
}