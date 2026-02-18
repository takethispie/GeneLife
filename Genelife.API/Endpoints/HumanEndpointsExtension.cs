using Genelife.Life.Generators;
using Genelife.Life.Messages.Commands;
using Genelife.Life.Messages.DTOs;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class HumanEndpointsExtension
{
    public static void UseHumanEndpoints(this WebApplication app)
    {
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
    }
}