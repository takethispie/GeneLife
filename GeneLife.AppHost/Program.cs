var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var genelifeDb = postgres.AddDatabase("genelife");

var api = builder.AddDotnetProject("api", "../Genelife.API/Genelife.API.csproj", options =>
    {
        options.LaunchProfileName = "http";
    })
    .WithHttpEndpoint(port: 5288, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(genelifeDb)
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("web", "../Genelife.Web/Genelife.Web.csproj", options =>
    {
        options.LaunchProfileName = "http";
    })
    .WithHttpEndpoint(port: 5068, name: "http")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
