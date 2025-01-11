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

var _ = builder.AddProject<Projects.portal_fluent>("portal-fluent");
//var _ = builder.AddProject<Projects.api_integration_tests>("api-integration-tests")
//               .WithEnvironment("API_CONNECTION_NAME", api.Resource.Name)
//               .WithEnvironment("COSMOS_ACCOUNT_ENDPOINT", bicep.GetOutput("cosmosAccountEndpoint"))
//               .WithEnvironment("COSMOS_DATABASE_NAME", bicep.GetOutput("cosmosDatabaseName"))
//               .WithEnvironment("COSMOS_ORDERS_CONTAINER_NAME", bicep.GetOutput("cosmosOrdersContainerName"))
//               .WithReference(api)
//               .WaitFor(api);


//var cosmosConnectionNameConfigurationKey = "ASPIRE_COSMOS_CONNECTION_NAME";
//var cosmosConnectionName = builder.Configuration.GetValue(cosmosConnectionNameConfigurationKey).IfNone("cosmos");
//var cosmos = builder.AddAzureCosmosDB(cosmosConnectionName).WithConnectionStringRedirection(builder.AddConnectionString("abc").Resource);

//var cosmosDatabaseConfigurationKey = "COSMOS_DATABASE_NAME";
//var cosmosDatabaseName = builder.Configuration.GetValue(cosmosDatabaseConfigurationKey).IfNone("epizzas");
//var cosmosDatabase = cosmos.AddDatabase(cosmosDatabaseName);
////.RunAsEmulator(resource => resource.WithLifetime(ContainerLifetime.Session));

//var cosmosOrdersContainerNameConfigurationKey = "COSMOS_ORDERS_CONTAINER_NAME";
//var cosmosOrdersContainerName = builder.Configuration.GetValue(cosmosOrdersContainerNameConfigurationKey).IfNone("orders");

//var apiConnectionNameConfigurationKey = "ASPIRE_API_CONNECTION_NAME";
//var apiProjectName = builder.Configuration.GetValue(apiConnectionNameConfigurationKey).IfNone("api");
//var api = builder.AddProject<Projects.api>(apiProjectName)
//                 .WithEnvironment(cosmosConnectionNameConfigurationKey, cosmosConnectionName)
//                 .WithEnvironment(cosmosDatabaseConfigurationKey, cosmosDatabaseName)
//                 .WithEnvironment(cosmosOrdersContainerNameConfigurationKey, cosmosOrdersContainerName)
//                 .WithReference(cosmosDatabase)
//                 .WaitFor(cosmosDatabase);

//var apiIntegrationTestsConnectionNameConfigurationKey = "ASPIRE_API_INTEGRATION_TESTS_CONNECTION_NAME";
//var apiIntegrationTestsConnectionName = builder.Configuration.GetValue(apiIntegrationTestsConnectionNameConfigurationKey).IfNone("api-integration-tests");
//var apiIntegrationTests = builder.AddProject<Projects.api_integration_tests>(apiIntegrationTestsConnectionName)
//                                 .WithEnvironment(cosmosConnectionNameConfigurationKey, cosmosConnectionName)
//                                 .WithEnvironment(cosmosDatabaseConfigurationKey, cosmosDatabaseName)
//                                 .WithEnvironment(cosmosOrdersContainerNameConfigurationKey, cosmosOrdersContainerName)
//                                 .WithReference(cosmosDatabase)
//                                 .WaitFor(cosmosDatabase)
//                                 .WithEnvironment(apiConnectionNameConfigurationKey, apiProjectName)
//                                 .WithReference(api)
//                                 .WaitFor(api);

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
