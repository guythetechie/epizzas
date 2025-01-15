using Aspire.Hosting;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

var builder = DistributedApplication.CreateBuilder(args);
var cancellationToken = CancellationToken.None;

var bicep = builder.AddBicepTemplate("bicep", "main.bicep")
                   .WithParameter("allowedIpCsv", await getIPAddress(cancellationToken))
                   .WithParameter(AzureBicepResource.KnownParameters.Location)
                   .WithParameter(AzureBicepResource.KnownParameters.PrincipalId);

var api = builder.AddProject<Projects.api>("api")
                 .WithEnvironment("COSMOS_ACCOUNT_ENDPOINT", bicep.GetOutput("cosmosAccountEndpoint"))
                 .WithEnvironment("COSMOS_DATABASE_NAME", bicep.GetOutput("cosmosDatabaseName"))
                 .WithEnvironment("COSMOS_ORDERS_CONTAINER_NAME", bicep.GetOutput("cosmosOrdersContainerName"));

var portal = builder.AddProject<Projects.portal>("portal")
                    .WithEnvironment("API_CONNECTION_NAME", api.Resource.Name)
                    .WithReference(api)
                    .WaitFor(api);

var integrationTests = builder.AddProject<Projects.api_integration_tests>("api-integration-tests")
                              .WithEnvironment("COSMOS_ACCOUNT_ENDPOINT", bicep.GetOutput("cosmosAccountEndpoint"))
                              .WithEnvironment("COSMOS_DATABASE_NAME", bicep.GetOutput("cosmosDatabaseName"))
                              .WithEnvironment("COSMOS_ORDERS_CONTAINER_NAME", bicep.GetOutput("cosmosOrdersContainerName"))
                              .WithEnvironment("API_CONNECTION_NAME", api.Resource.Name)
                              .WithReference(api)
                              .WaitFor(api);

await builder.Build().RunAsync(cancellationToken);

static async ValueTask<string> getIPAddress(CancellationToken cancellationToken)
{
    var services = new ServiceCollection().AddHttpClient();
    using var provider = services.BuildServiceProvider();
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    using var client = factory.CreateClient();

    var uri = new Uri("https://api.ipify.org");
    return await client.GetStringAsync(uri, cancellationToken);
}