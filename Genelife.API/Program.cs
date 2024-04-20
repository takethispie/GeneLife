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

app.MapGet("/healthcheck", (HttpContext httpContext) => Results.Ok()).WithName("healthcheck").WithOpenApi();


app.MapPost("/create/human/{sex}", (Sex sex, [FromServices] IPublishEndpoint endpoint) => {
    var human = HumanGenerator.Build(sex);
    endpoint.Publish(new CreateHuman(human.CorrelationId, human, Vector3.Zero));
    return Results.Ok(human.CorrelationId);
})
.WithName("createHuman")
.WithOpenApi();


app.MapPost("/create/groceryShop/{x}/{y}", (int x, int y, [FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    endpoint.Publish(new CreateGroceryShop(guid, x, y, new Vector2(50, 50)));
    return Results.Ok(guid);
})
.WithName("createGrocery")
.WithOpenApi();


app.MapPost("/create/city/small", ([FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    endpoint.Publish(new CreateGroceryShop(guid, 500, 500, new Vector2(50, 50)));
    var human = HumanGenerator.Build(Sex.Male);
    endpoint.Publish(new CreateHuman(human.CorrelationId, human, Vector3.Zero));
    human = HumanGenerator.Build(Sex.Male);
    endpoint.Publish(new CreateHuman(human.CorrelationId, human, new Vector3(50, 100, 0)));
    human = HumanGenerator.Build(Sex.Female);
    endpoint.Publish(new CreateHuman(human.CorrelationId, human, new Vector3(100, 200, 0)));
    return Results.Ok(guid);
})
.WithName("createSmallCity")
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


app.MapGet("/simulation/setClockSpeed/{milliseconds}", (int milliseconds, [FromServices] IPublishEndpoint endpoint) => {
    endpoint.Publish(new SetClockSpeed(milliseconds));
})
.WithName("setClockSpeed")
.WithOpenApi();


app.MapGet("/action/go/{correlationId}/groceryShop", (Guid correlationId, [FromServices] IPublishEndpoint endpoint) => {
    endpoint.Publish(new GoToGroceryShop(correlationId));
})
.WithName("goToGroceryShop")
.WithOpenApi();


app.Run();
