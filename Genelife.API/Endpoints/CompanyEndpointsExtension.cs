using System.Numerics;
using Genelife.API.DTOs;
using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Messages.Commands.Company;
using Genelife.Messages.Commands.Jobs;
using Genelife.Messages.Events.Jobs;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class CompanyEndpointsExtension
{
    public static void UseCompanyEndpoints(this WebApplication app)
    {
        app.MapPost("/create/company/{type}", async (CompanyType type, Guid officeId, [FromServices] IPublishEndpoint endpoint) =>
            {
                var companyId = Guid.NewGuid();
                var company = new Company(
                    Name: $"{type} Corp {Random.Shared.Next(1000, 9999)}",
                    Type: type,
                    Revenue: 50000 + Random.Shared.NextSingle() * 100000,
                    TaxRate: 0.25f,
                    EmployeeIds: []
                );
    
                var officeLocation = new Vector3(
                    Random.Shared.NextSingle() * 800 - 400, 
                    Random.Shared.NextSingle() * 800 - 400,
                    0
                );

                await endpoint.Publish(new CreateCompany(companyId, company, officeLocation.X, officeLocation.Y, officeLocation.Z));
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
    }
}