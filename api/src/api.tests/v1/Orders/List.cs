using Aspire.Hosting;
using Aspire.Hosting.Testing;
using common;
using DotNext.Threading;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace api.tests.v1.Orders;

public sealed class ListTests : IAsyncLifetime
{
    private readonly AsyncLazy<DistributedApplication> distributedApplication = new(async cancellationToken => await CommonModule.GetDistributedApplication(cancellationToken));

    private readonly Uri uri = new("/orders", UriKind.Relative);

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await CreateContainerIfNotExists(cancellationToken);
    }

    [Fact]
    public void A()
    {
    }

    private async ValueTask CreateContainerIfNotExists(CancellationToken cancellationToken)
    {
        var application = await distributedApplication.WithCancellation(cancellationToken);
        var configuration = application.Services.GetRequiredService<IConfiguration>();

        using var client = new CosmosClient(configuration["COSMOS_CONNECTION_STRING"]);

        var databaseName = configuration.GetValueOrThrow("COSMOS_DATABASE_NAME");
        var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseName, throughput: 4000, cancellationToken: cancellationToken);
        var database = databaseResponse.Database;

        var containerName = configuration.GetValueOrThrow("COSMOS_ORDERS_CONTAINER_NAME");
        var containerResponse = await database.CreateContainerIfNotExistsAsync(containerName, "/orderId", throughput: 400, cancellationToken: cancellationToken);
        var container = containerResponse.Container;

        Console.WriteLine(container);
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var application = await distributedApplication.WithCancellation(cancellationToken);
        await application.StopAsync(cancellationToken);
        await application.DisposeAsync();
    }

    [Fact]
    public async ValueTask List_returns_expected_value()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var application = await distributedApplication.WithCancellation(cancellationToken);

        var apiProjectName = await CommonModule.GetApiProjectName.WithCancellation(cancellationToken);
        using var client = application.CreateHttpClient(apiProjectName);
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

//public sealed class ListTests2IDisposable
//{
//    [Fact]
//    public async Task List_returns_expected_results()
//    {
//        var generator = Fixture.Generate;

//        await generator.SampleAsync(async fixture =>
//        {
//            // Arrange
//            var cancellationToken = CancellationToken.None;
//            using var factory = new ApplicationFactory(fixture);
//            using var client = factory.CreateClient();

//            // Act
//            var response = await client.GetAsync(new Uri("/v1/orders"), cancellationToken);
//        }, iter: 1);
//    }

//    private sealed record Fixture
//    {
//        public ImmutableArray<Order> Orders { get; init; }

//        public static Gen<Fixture> Generate =>
//            from orders in Generator.Order.ImmutableArrayOf()
//            select new Fixture
//            {
//                Orders = orders
//            };
//    }

//    private sealed class ApplicationFactory(Fixture fixture) : WebApplicationFactory<api.Program>, IAsyncLifetime
//    {
//        private readonly DistributedApplication distributedApplication = CreateDistributedApplication();

//        private static DistributedApplication CreateDistributedApplication()
//        {
//            var options = new DistributedApplicationOptions
//            {
//                AssemblyName = typeof(ApplicationFactory).Assembly.FullName,
//                DisableDashboard = true,
//                Args = []
//            };

//            var builder = DistributedApplication.CreateBuilder(options);
//            builder.Configuration.AddUserSecrets(typeof(Fixture).Assembly);
//            var cosmosConnectionName = builder.Configuration.GetValueOrThrow("ASPIRE_COSMOS_CONNECTION_NAME");
//            var cosmos = builder.AddAzureCosmosDB(cosmosConnectionName);

//            var cosmosDatabaseName = builder.Configuration.GetValueOrThrow("COSMOS_DATABASE_NAME");
//            var cosmosDatabase = cosmos.AddDatabase(string.Empty)
//                                       .RunAsEmulator(resource => resource.WithDataVolume()
//                                                                          .WithLifetime(ContainerLifetime.Persistent));

//            return builder.Build();
//        }

//        protected override IHost CreateHost(IHostBuilder builder)
//        {
//            builder.ConfigureServices(services =>
//            {
//                services.AddSingleton(fixture);
//                services.AddHostedService<SeedDatabaseService>();
//            });

//            return base.CreateHost(builder);
//        }

//        public async Task InitializeAsync()
//        {
//            var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.aspire>();
//            var a = await builder.BuildAsync();
//            Console.Write(a);

//            await distributedApplication.StartAsync();
//        }

//        public override async ValueTask DisposeAsync()
//        {
//            await base.DisposeAsync();
//            await distributedApplication.StopAsync();
//            await distributedApplication.DisposeAsync();
//        }

//        async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
//    }

//#pragma warning disable CA1812 // Avoid uninstantiated internal classes
//    private sealed class SeedDatabaseService(IServiceProvider provider, Fixture fixture) : IHostedService
//#pragma warning restore CA1812 // Avoid uninstantiated internal classes
//    {
//        private readonly IConfiguration configuration = provider.GetRequiredService<IConfiguration>();

//        public async Task StartAsync(CancellationToken cancellationToken)
//        {
//            var container = await GetOrCreateContainer(cancellationToken);

//            await fixture.Orders
//                         .IterParallel(async order =>
//                         {
//                             var cosmosId = CosmosId.Generate();
//                             var partitionKey = api.v1.Orders.CosmosModule.GetPartitionKey(order);
//                             var json = Order.Serialize(order).SetProperty("id", cosmosId.ToString());
//                             await CosmosModule.CreateRecord(container, json, partitionKey).RunUnsafe(cancellationToken);
//                         }, cancellationToken);
//        }

//        private async ValueTask<Container> GetOrCreateContainer(CancellationToken cancellationToken)
//        {
//            var client = provider.GetRequiredService<CosmosClient>();

//            var databaseName = configuration.GetValueOrThrow("COSMOS_DATABASE_NAME");
//            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseName, throughput: 4000, cancellationToken: cancellationToken);
//            var database = databaseResponse.Database;

//            var containerName = configuration.GetValueOrThrow("COSMOS_ORDERS_CONTAINER_NAME");
//            var containerResponse = await database.CreateContainerIfNotExistsAsync(containerName, "/orderId", throughput: 400, cancellationToken: cancellationToken);
//            var container = containerResponse.Container;

//            return container;
//        }

//        public async Task StopAsync(CancellationToken cancellationToken)
//        {
//            var container = await GetOrCreateContainer(cancellationToken);

//            await container.DeleteContainerAsync(cancellationToken: cancellationToken);
//        }
//    }
//}

////file class AppHost() : DistributedApplicationFactory(typeof(Projects.aspire))
////{
////    protected override void OnBuilderCreating(DistributedApplicationOptions applicationOptions, HostApplicationBuilderSettings hostOptions)
////    {
////        var configuration = hostOptions.Configuration!;
////        configuration["AZURE_SUBSCRIPTION_ID"] = "00000000-0000-0000-0000-000000000000";
////    }
////}

////public class TestingAspireAppHost() : DistributedApplicationFactory(typeof(Projects.aspire))
////{
////    protected override void OnBuilderCreating(DistributedApplicationOptions applicationOptions, HostApplicationBuilderSettings hostOptions)
////    {
////        builder.EnvironmentVariables["AZURE_SUBSCRIPTION_ID"] = "00000000-0000-0000-0000-000000000000";
////        builder.EnvironmentVariables["AZURE_RESOURCE_GROUP"] = "my-resource-group";
////    }

////    protected override void OnBuilderCreated(DistributedApplicationBuilder builder)
////    {
////        builder.
////    }
////}