using Genelife.Messages.Commands.Clock;
using MassTransit;
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


        app.MapGet("/simulation/setClockSpeed/{milliseconds}", async (int milliseconds, [FromServices] IPublishEndpoint endpoint) => {
                await endpoint.Publish(new SetClockSpeed(milliseconds));
            })
            .WithName("set Clock Speed");
    }
}