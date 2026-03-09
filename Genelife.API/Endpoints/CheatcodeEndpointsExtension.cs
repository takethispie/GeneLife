using Genelife.Messages.Commands;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class CheatcodeEndpointsExtension
{
    public static void UseCheatcodeEndpoints(this WebApplication app)
    {
        app.MapGet("/cheat/sethunger/{correlationId:guid}/{value:int}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) =>
            {
                if(value <= 0) return Results.BadRequest("hunger Should be greater than 0");
                await endpoint.Publish(new SetHunger(correlationId, value));
                return Results.Ok();
            })
            .WithName("set Hunger");

        app.MapGet("/cheat/setage/{correlationId:guid}/{value:int}", async (Guid correlationId, int value, [FromServices] IPublishEndpoint endpoint) =>
            {
                if(value <= 0) return Results.BadRequest("age Should be greater than 0");
                await endpoint.Publish(new SetHunger(correlationId, value));
                return Results.Ok();
            })
            .WithName("set Age");
    }
}