var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.api>("api");

builder.Build().Run();
