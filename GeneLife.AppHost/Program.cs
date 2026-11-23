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

// The browser client is a Vite dev server. Pin the host port to 5173 so the
// origin stays in the API's CORS allow-list, and point it at the API's
// external endpoint so the browser (not the AppHost) can reach SignalR.
builder.AddViteApp("browser", "../clients/browser")
    .WithEndpoint("http", endpoint => endpoint.Port = 5173)
    .WithExternalHttpEndpoints()
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
