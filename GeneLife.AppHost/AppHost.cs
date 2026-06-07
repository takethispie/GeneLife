var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Application>("application");

builder.Build().Run();
