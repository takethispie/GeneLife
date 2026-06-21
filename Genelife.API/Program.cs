using Genelife.Api.Services;
using Genelife.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register SimulationManager as singleton
builder.Services.AddSingleton<SimulationManager>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationManager>());

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapSimulationEndpoints();
app.MapCompanyEndpoints();

app.Run();
