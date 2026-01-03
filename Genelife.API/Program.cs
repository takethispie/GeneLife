using System.Reflection;
using System.Numerics;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Genelife.API.DTOs;
using Genelife.Global.Messages.Commands.Clock;
using Genelife.Global.Messages.Events.Buildings;
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
builder.Services.AddOpenApi();
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

app.MapGet("/healthcheck", (HttpContext _) => Results.Ok()).WithName("healthcheck");


app.MapPost("/create/human/{sex}", async (Sex sex, [FromServices] IPublishEndpoint endpoint) => {
        var guid = Guid.NewGuid();
        await endpoint.Publish(new CreateHuman(guid, HumanGenerator.Build(sex)));
    })
    .WithName("create Human");


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
.WithName("create Multiple Humans");


app.MapGet("/simulation/start", async ([FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new StartClock());
})
.WithName("simulation Start");


app.MapGet("/simulation/stop", async ([FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new StopClock());
})
.WithName("simulation Stop");


app.MapGet("/simulation/setClockSpeed/{milliseconds}", async (int milliseconds, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new SetClockSpeed(milliseconds));
})
.WithName("set Clock Speed");


app.MapGet("/cheat/sethunger/{correlationId}/{value}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) =>
{
    await endpoint.Publish(new SetHunger(correlationId, value));
})
.WithName("set Hunger");

app.MapGet("/cheat/setage/{correlationId}/{value:int}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) =>
    {
        await endpoint.Publish(new SetHunger(correlationId, value));
    })
    .WithName("set Age");


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
.WithName("create Company");


app.MapPost("/create/jobposting", async ([FromBody] JobPosting request, [FromServices] IPublishEndpoint endpoint) => {
    var id = Guid.NewGuid();
    await endpoint.Publish(new CreateJobPosting(id, request));
    return Results.Ok("Job posting created");
})
.WithName("create Job Posting");


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
.WithName("submit Job Application");


app.MapPost("/create/house", async ([FromBody] CreateHouseRequest request, [FromServices] IPublishEndpoint endpoint) =>
{
    var owners = new List<Guid> { request.HumanId };
    if (request.AdditionalOwners != null)
    {
        owners.AddRange(request.AdditionalOwners);
    }

    await endpoint.Publish(new HouseBuilt(
        request.HumanId,
        request.Location.X,
        request.Location.Y,
        request.Location.Z,
        owners
    ));
    
    return Results.Ok(new {
        Message = "House created successfully",
        request.HumanId,
        request.Location,
        Owners = owners
    });
})
.WithName("create House");


app.MapPost("/create/office", async ([FromBody] CreateOfficeRequest request, [FromServices] IPublishEndpoint endpoint) =>
{
    var officeId = Guid.NewGuid();
    
    await endpoint.Publish(new OfficeCreated(
        officeId,
        request.Location.X,
        request.Location.Y,
        request.Location.Z,
        request.Name,
        request.OwningCompanyId
    ));
    
    return Results.Ok(new {
        Message = "Office created successfully",
        OfficeId = officeId,
        request.Name,
        request.Location,
        request.OwningCompanyId
    });
})
.WithName("create Office");


app.MapPost("/create/population/{humanCount}", async (int humanCount, [FromServices] IPublishEndpoint endpoint) =>
{
    var results = new
    {
        Humans = new List<object>(),
        Companies = new List<object>(),
        Houses = new List<object>(),
        Offices = new List<object>()
    };

    // Create humans with random sex distribution and houses for them
    for (int i = 0; i < humanCount; i++)
    {
        var sex = Random.Shared.Next(2) == 0 ? Sex.Male : Sex.Female;
        var humanId = Guid.NewGuid();
        var human = HumanGenerator.Build(sex);
        
        await endpoint.Publish(new CreateHuman(humanId, human));
        results.Humans.Add(new { HumanId = humanId, Human = human });

        var houseLocation = new Vector3(
            Random.Shared.NextSingle() * 1000 - 500,
            Random.Shared.NextSingle() * 1000 - 500,
            0
        );
        
        await endpoint.Publish(new HouseBuilt(
            humanId,
            houseLocation.X,
            houseLocation.Y,
            houseLocation.Z,
            [humanId]
        ));
        
        results.Houses.Add(new {
            HumanId = humanId,
            Location = houseLocation,
            Owners = new List<Guid> { humanId }
        });
    }

    var companyTypes = Enum.GetValues<CompanyType>().Shuffle().Take(2).ToArray();
    foreach (var companyType in companyTypes)
    {
        var company = CompanyGenerator.Generate(companyType);
        var companyId = Guid.NewGuid();
        await endpoint.Publish(new CreateCompany(companyId, company));
        results.Companies.Add(new { CompanyId = companyId, Company = company });

        // Create an office for each company at a random location
        var officeLocation = new Vector3(
            // X: -400 to 400 (closer to center for business district)
            Random.Shared.NextSingle() * 800 - 400, 
            Random.Shared.NextSingle() * 800 - 400,
            0
        );
        
        var officeId = Guid.NewGuid();
        await endpoint.Publish(new OfficeCreated(
            officeId,
            officeLocation.X,
            officeLocation.Y,
            officeLocation.Z,
            $"{company.Name} Headquarters",
            companyId
        ));
        
        results.Offices.Add(new {
            OfficeId = officeId,
            CompanyId = companyId,
            Name = $"{company.Name} Headquarters",
            Location = officeLocation
        });
    }
    
    await endpoint.Publish(new SetClockSpeed(100));
    await endpoint.Publish(new StartClock());

    return Results.Ok(new
    {
        Message = $"Created {humanCount} humans with houses, {companyTypes.Length} companies with offices, set clock speed to 100ms, started simulation",
        CreatedHumans = humanCount,
        CreatedCompanies = companyTypes.Length,
        CreatedHouses = humanCount,
        CreatedOffices = companyTypes.Length,
        Details = results
    });
})
.WithName("create Population");

app.Run();
