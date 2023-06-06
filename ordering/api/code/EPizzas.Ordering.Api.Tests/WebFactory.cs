using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace EPizzas.Ordering.Api.Tests;

internal sealed class WebFactory : WebApplicationFactory<Program>
{
    public Action<IServiceCollection> ConfigureServices { get; init; } = _ => { };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(ConfigureServices);
    }
}
