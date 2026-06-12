var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("genelifedb");

var api = builder.AddProject<Projects.Application>("application")
    .WithReference(postgres);

// Add AdminPanel as a regular project (Blazor WASM will be served by its dev server)
var adminPanel = builder.AddProject<Projects.AdminPanel>("adminpanel")
    .WithExternalHttpEndpoints()
    .WithReference(api);

builder.Build().Run();
