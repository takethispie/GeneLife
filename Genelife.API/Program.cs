using System.Reflection;
using Genelife.Domain.Commands;
using Genelife.Domain.Generators;
using Genelife.Domain;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;

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

app.MapGet("/healthcheck", (HttpContext httpContext) => Results.Ok())
.WithName("healthcheck")
.WithOpenApi();


app.MapPost("/create/human/{sex}", ([FromQuery]Sex sex, [FromServices] IPublishEndpoint endpoint) => {
    var human = HumanGenerator.Build(sex);
    endpoint.Publish(new CreateHuman(human.CorrelationId, human));
    return Results.Ok(human.CorrelationId);
})
.WithName("createHuman")
.WithOpenApi();


app.MapPost("/create/groceryShop/{x}/{y}", ([FromQuery]float x, [FromQuery] float y, [FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    endpoint.Publish(new CreateGroceryShop(guid, new Vector3(x, y, 0), new Vector2(50, 50)));
    return Results.Ok(guid);
})
.WithName("createGrocery")
.WithOpenApi();


app.MapGet("/simulation/start", ([FromServices] IPublishEndpoint endpoint) => {
    endpoint.Publish(new StartClock());
})
.WithName("simulationStart")
.WithOpenApi();


app.MapGet("/simulation/stop", ([FromServices] IPublishEndpoint endpoint) => {
    endpoint.Publish(new StopClock());
})
.WithName("simulationStop")
.WithOpenApi();


app.MapGet("/simulation/setClockSpeed/{milliseconds}", ([FromQuery] int milliseconds, [FromServices] IPublishEndpoint endpoint) => {
    endpoint.Publish(new SetClockSpeed(milliseconds));
})
.WithName("setClockSpeed")
.WithOpenApi();


app.Run();
