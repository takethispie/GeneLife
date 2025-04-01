using System.Reflection;
using Genelife.Domain;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Genelife.Domain.Commands.Cheat;
using Genelife.Domain.Commands.Clock;
using Genelife.Domain.Events.Living;
using Genelife.Domain.Generators;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMassTransit(x => {
    x.SetKebabCaseEndpointNameFormatter();
    x.SetInMemorySagaRepositoryProvider();
    var entryAssembly = Assembly.GetEntryAssembly();
    x.AddConsumers(entryAssembly);
    x.AddSagaStateMachines(entryAssembly);
    x.AddSagas(entryAssembly);
    x.AddActivities(entryAssembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        if (IsRunningInContainer())
            cfg.Host("rabbitmq");
        cfg.UseDelayedMessageScheduler();
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthorization();

app.MapGet("/healthcheck", (HttpContext httpContext) => Results.Ok()).WithName("healthcheck").WithOpenApi();


app.MapPost("/create/human/{sex}", async (Sex sex, [FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    await endpoint.Publish(new CreateHuman(guid, HumanGenerator.Build(sex)));
})
.WithName("create Human")
.WithOpenApi();

app.MapPost("/create/human/{count}/{sex}", async (int count, Sex sex, [FromServices] IPublishEndpoint endpoint) => {
    
    var guid = Guid.NewGuid();
    await endpoint.Publish(new CreateHuman(guid, HumanGenerator.Build(sex)));
})
.WithName("create Human")
.WithOpenApi();


app.MapGet("/simulation/start", async ([FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new StartClock());
})
.WithName("simulation Start")
.WithOpenApi();


app.MapGet("/simulation/stop", async ([FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new StopClock());
})
.WithName("simulation Stop")
.WithOpenApi();


app.MapGet("/simulation/setClockSpeed/{milliseconds}", async (int milliseconds, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new SetClockSpeed(milliseconds));
})
.WithName("set Clock Speed")
.WithOpenApi();


app.MapGet("/cheat/sethunger/{correlationId}/{value}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new SetHunger(correlationId, value));
})
.WithName("set Hunger")
.WithOpenApi();

app.Run();