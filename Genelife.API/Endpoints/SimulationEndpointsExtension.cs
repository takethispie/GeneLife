using Genelife.Messages.Commands.Clock;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Genelife.API.Endpoints;

public static class SimulationEndpointsExtension
{
    public static void UseSimulationEndpoints(this WebApplication app)
    {
        app.MapGet("/simulation/start", async ([FromServices] IPublishEndpoint endpoint) => {
                await endpoint.Publish(new StartClock());
            })
            .WithName("simulation Start");


        app.MapGet("/simulation/stop", async ([FromServices] IPublishEndpoint endpoint) => {
                await endpoint.Publish(new StopClock());
            })
            .WithName("simulation Stop");


        app.MapGet("/simulation/setClockSpeed/{seconds:int}", async (int seconds, [FromServices] IPublishEndpoint endpoint) =>
            {
                if (seconds >= 0) return Results.BadRequest("Should be greater than 0");
                await endpoint.Publish(new SetClockSpeed(seconds));
                return Results.Ok();
            })
            .WithName("set Clock Speed");
    }
}