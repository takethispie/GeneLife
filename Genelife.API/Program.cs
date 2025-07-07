using System.Reflection;
using Genelife.Domain;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Genelife.Domain.Commands.Cheat;
using Genelife.Domain.Commands.Clock;
using Genelife.Domain.Commands.Company;
using Genelife.Domain.Commands.Jobs;
using Genelife.Domain.Events.Living;
using Genelife.Domain.Generators;
using CreateCompanyEvent = Genelife.Domain.Events.Company.CompanyCreated;
using Genelife.API.DTOs;

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

app.MapPost("/create/company/{type}", async (CompanyType type, [FromServices] IPublishEndpoint endpoint) => {
    var companyId = Guid.NewGuid();
    var company = new Company(
        Id: companyId,
        Name: $"{type} Corp {Random.Shared.Next(1000, 9999)}",
        Type: type,
        Revenue: 50000m + (decimal)(Random.Shared.NextDouble() * 100000),
        TaxRate: 0.25m,
        EmployeeIds: []
    );
    
    await endpoint.Publish(new CreateCompanyEvent(companyId, company));
    return Results.Ok(new { CompanyId = companyId, Company = company });
})
.WithName("create Company")
.WithOpenApi();

app.MapPost("/create/jobposting", async ([FromBody] CreateJobPostingRequest request, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new CreateJobPosting(
        request.CompanyId,
        request.Title,
        request.Description,
        request.Requirements,
        request.SalaryMin,
        request.SalaryMax,
        request.Level,
        request.MaxApplications,
        request.DaysToExpire
    ));
    return Results.Ok("Job posting created");
})
.WithName("create Job Posting")
.WithOpenApi();

app.MapPost("/submit/application", async ([FromBody] SubmitJobApplicationRequest request, [FromServices] IPublishEndpoint endpoint) => {
    await endpoint.Publish(new SubmitJobApplication(
        request.JobPostingId,
        request.HumanId,
        request.RequestedSalary,
        request.CoverLetter ?? "I am interested in this position.",
        request.Skills,
        request.Experience
    ));
    return Results.Ok("Application submitted");
})
.WithName("submit Job Application")
.WithOpenApi();

app.Run();
