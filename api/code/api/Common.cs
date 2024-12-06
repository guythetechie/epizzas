using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

namespace api;

internal static class CommonModule
{
    public static void ConfigureTimeProvider(IHostApplicationBuilder builder) =>
        builder.Services.TryAddSingleton(TimeProvider.System);
}
