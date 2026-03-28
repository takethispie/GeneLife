using Genelife.API.DTOs;
using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Domain.Human;
using Genelife.Domain.Locations;
using Genelife.Messages.Commands;
using Genelife.Messages.Commands.Human;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class HumanEndpointsExtension
{
    public static void UseHumanEndpoints(this WebApplication app)
    {
        app.MapPost("/create/human/{sex}", async (Sex sex, [FromServices] IPublishEndpoint endpoint) => {
                var guid = Guid.NewGuid();
                await endpoint.Publish(new CreateHuman(guid, HumanGenerator.Build(guid, sex)));
            })
            .WithName("create Human");


        app.MapPost("/create/human/{count:int}/{sex}", async (int count, Sex sex, [FromServices] IPublishEndpoint endpoint) =>
            {
                if(count <= 0) return Results.BadRequest("human count Should be greater than 0");
                var results = new List<object>();
    
                for (int i = 0; i < count; i++)
                {
                    var humanId = Guid.NewGuid();
                    var human = HumanGenerator.Build(humanId, sex);
                    await endpoint.Publish(new CreateHuman(humanId, human));
                    results.Add(new { HumanId = humanId, Human = human });
                }
    
                return Results.Ok(new { CreatedCount = count, Humans = results });
            })
            .WithName("create Multiple Humans");

        app.MapPost("/create/human/custom", async ([FromBody] CreateHumanRequest request, [FromServices] IPublishEndpoint endpoint) =>
            {
                if (string.IsNullOrWhiteSpace(request.FirstName))
                    return Results.BadRequest("FirstName is required.");
                if (string.IsNullOrWhiteSpace(request.LastName))
                    return Results.BadRequest("LastName is required.");
                if (request.Age <= 0)
                    return Results.BadRequest("Age must be greater than 0.");
                if (request.Money < 0)
                    return Results.BadRequest("Money cannot be negative.");

                var humanId = Guid.NewGuid();
                var person = new Person(
                    humanId,
                    request.FirstName,
                    request.LastName,
                    DateTime.Now.AddYears(-request.Age),
                    request.Sex,
                    new LifeSkillSet(),
                    new Position(0, 0, 0),
                    new AddressBook(),
                    request.Money
                );

                await endpoint.Publish(new CreateHuman(humanId, person));
                return Results.Ok(new { HumanId = humanId });
            })
            .WithName("create Custom Human");
    }
}