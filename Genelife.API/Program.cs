using System.Reflection;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Genelife.API.DTOs;
using Genelife.Global.Messages.Commands.Clock;
using Genelife.Life.Generators;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Messages.DTOs;
using Genelife.Work.Generators;
using Genelife.Work.Messages.Commands.Company;
using Genelife.Work.Messages.Commands.Jobs;
using Genelife.Work.Messages.DTOs;
using Genelife.Work.Messages.Events.Jobs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

static bool IsRunningInContainer() => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var inContainer) && inContainer;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
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


app.MapPost("/create/human/{sex}", async (Sex sex, [FromServices] IPublishEndpoint endpoint) =>
{
    var guid = Guid.NewGuid();
    await endpoint.Publish(new CreateHuman(guid, HumanGenerator.Build(sex)));
})
.WithName("create Human")
.WithOpenApi();


app.MapPost("/create/human/{count}/{sex}", async (int count, Sex sex, [FromServices] IPublishEndpoint endpoint) =>
{
    var results = new List<object>();
    
    for (int i = 0; i < count; i++)
    {
        var humanId = Guid.NewGuid();
        var human = HumanGenerator.Build(sex);
        await endpoint.Publish(new CreateHuman(humanId, human));
        results.Add(new { HumanId = humanId, Human = human });
    }
    
    return Results.Ok(new { CreatedCount = count, Humans = results });
})
.WithName("create Multiple Humans")
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


app.MapGet("/cheat/sethunger/{correlationId}/{value}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) =>
{
    await endpoint.Publish(new SetHunger(correlationId, value));
})
.WithName("set Hunger")
.WithOpenApi();

app.MapGet("/cheat/setage/{correlationId}/{value:int}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) =>
    {
        await endpoint.Publish(new SetHunger(correlationId, value));
    })
    .WithName("set Age")
    .WithOpenApi();


app.MapPost("/create/company/{type}", async (CompanyType type, [FromServices] IPublishEndpoint endpoint) =>
{
    var companyId = Guid.NewGuid();
    var company = new Company(
        Name: $"{type} Corp {Random.Shared.Next(1000, 9999)}",
        Type: type,
        Revenue: 50000 + Random.Shared.NextSingle() * 100000,
        TaxRate: 0.25f,
        EmployeeIds: []
    );

    await endpoint.Publish(new CreateCompany(companyId, company));
    return Results.Ok(new { CompanyId = companyId, Company = company });
})
.WithName("create Company")
.WithOpenApi();


app.MapPost("/create/jobposting", async ([FromBody] JobPosting request, [FromServices] IPublishEndpoint endpoint) => {
    var id = Guid.NewGuid();
    await endpoint.Publish(new CreateJobPosting(id, request));
    return Results.Ok("Job posting created");
})
.WithName("create Job Posting")
.WithOpenApi();


app.MapPost("/submit/application", async ([FromBody] SubmitJobApplicationRequest request, [FromServices] IPublishEndpoint endpoint) =>
{
    await endpoint.Publish(new JobApplicationSubmitted(
        request.JobPostingId,
        new JobApplication(
            request.JobPostingId,
            request.HumanId,
            DateTime.Now,
            request.RequestedSalary,
            request.SkillSet,
            request.Experience)
    ));
    return Results.Ok("Application submitted");
})
.WithName("submit Job Application")
.WithOpenApi();


app.MapPost("/create/population/{humanCount}", async (int humanCount, [FromServices] IPublishEndpoint endpoint) =>
{
    var results = new
    {
        Humans = new List<object>(),
        Companies = new List<object>()
    };

    // Create humans with random sex distribution
    for (int i = 0; i < humanCount; i++)
    {
        var sex = Random.Shared.Next(2) == 0 ? Sex.Male : Sex.Female;
        var humanId = Guid.NewGuid();
        var human = HumanGenerator.Build(sex);
        
        await endpoint.Publish(new CreateHuman(humanId, human));
        results.Humans.Add(new { HumanId = humanId, Human = human });
    }

    // Create companies of different types (one of each type)
    var companyTypes = Enum.GetValues<CompanyType>();
    foreach (var companyType in companyTypes)
    {
        var company = CompanyGenerator.Generate(companyType);
        var id = Guid.NewGuid();
        await endpoint.Publish(new CreateCompany(id, company));
        results.Companies.Add(new { CompanyId = id, Company = company });
    }

    return Results.Ok(new
    {
        Message = $"Created {humanCount} humans and {companyTypes.Length} companies",
        CreatedHumans = humanCount,
        CreatedCompanies = companyTypes.Length,
        Details = results
    });
})
.WithName("create Population")
.WithOpenApi();


app.Run();
