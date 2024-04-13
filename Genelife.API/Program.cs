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

app.Run();
