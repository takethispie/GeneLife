using Genelife.Application.Usecases;
using Genelife.Domain;
using Genelife.Messages.Commands;
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
    }
}