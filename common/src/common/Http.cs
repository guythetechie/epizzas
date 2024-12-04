using Microsoft.Extensions.DependencyInjection;

namespace common;

public static class HttpModule
{
    public static void ConfigureClientDefaults(IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.AddStandardResilienceHandler();
            builder.AddServiceDiscovery();
        });
    }
}