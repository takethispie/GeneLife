var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Genelife_API>("genelife-api");

builder.AddProject<Projects.Genelife_Application>("genelife-application");

builder.Build().Run();
