using Genelife.Life.Messages.Commands;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class CheatcodeEndpointsExtension
{
    public static void UseCheatcodeEndpoints(this WebApplication app)
    {
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
    }
}