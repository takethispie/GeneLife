using System.Reflection;
using Genelife.Domain.Commands;
using Genelife.Domain.Generators;
using Genelife.Domain;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

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
    var human = HumanGenerator.Build(sex);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 0, 0));
    return Results.Ok(human.CorrelationId);
})
.WithName("create Human")
.WithOpenApi();

app.MapPost("/create/human/{sex}/{hunger}/{thirst}", async (Sex sex, int hunger, int thirst, [FromServices] IPublishEndpoint endpoint) => {
    var human = HumanGenerator.Build(sex);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 0, 0));
    await endpoint.Publish(new SetHunger(human.CorrelationId, hunger));
    await endpoint.Publish(new SetThirst(human.CorrelationId, thirst));
    return Results.Ok(human.CorrelationId);
})
.WithName("create Human with specific hunger and thirst")
.WithOpenApi();


app.MapPost("/create/groceryShop/{x}/{y}", async (int x, int y, [FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    await endpoint.Publish(new CreateGroceryShop(guid, x, y, 50, 50));
    return Results.Ok(guid);
})
.WithName("create Grocery store")
.WithOpenApi();


app.MapPost("/create/city/small", async ([FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    await endpoint.Publish(new CreateGroceryShop(guid, 500, 500, 50, 50));
    var human = HumanGenerator.Build(Sex.Male);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 0, 0));
    human = HumanGenerator.Build(Sex.Male);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 50, 100));
    human = HumanGenerator.Build(Sex.Female);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 100, 200));
    return Results.Ok(guid);
})
.WithName("create Small City")
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


app.MapGet("/action/go/{correlationId}/groceryShop", (Guid correlationId, [FromServices] IPublishEndpoint endpoint) => {
    //await endpoint.Publish(new GoToGroceryShop(correlationId));
})
.WithName("go To Grocery Store")
.WithOpenApi();

app.MapGet("/cheat/sethunger/{correlationId}/{value}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new SetHunger(correlationId, value));
})
.WithName("set Hunger")
.WithOpenApi();

app.MapGet("/cheat/setthirst/{correlationId}/{value}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new SetThirst(correlationId, value));
})
.WithName("set thirst")
.WithOpenApi();

app.MapGet("/usecase/survivalNoFoodInventory", async ([FromServices] IPublishEndpoint endpoint) => {
    var guid = Guid.NewGuid();
    await endpoint.Publish(new CreateGroceryShop(guid, 500, 500, 50, 50));
    var human = HumanGenerator.Build(Sex.Male);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 0, 0, 9, 19));
    human = HumanGenerator.Build(Sex.Male);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 50, 100, 7, 15));
    human = HumanGenerator.Build(Sex.Female);
    await endpoint.Publish(new CreateHuman(human.CorrelationId, human, 100, 200, 9, 19));
    await endpoint.Publish(new SetClockSpeed(100));
    await endpoint.Publish(new StartClock());
    return Results.Ok();
})
.WithName("survival with no food in inventory usecase")
.WithOpenApi();


app.Run();
