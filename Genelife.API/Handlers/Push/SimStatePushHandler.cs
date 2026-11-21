using Genelife.Api.DTOs;
using Genelife.Api.Hubs;
using Genelife.Messages.Events;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Handlers.Push;

public class SimStatePushHandler
{
    private readonly IHubContext<WorldHub> hubContext;

    public SimStatePushHandler(IHubContext<WorldHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public async Task Handle(SimCreated @event)
    {
        await hubContext.Clients.Group("all-sims").SendAsync("SimCreated", new
        {
            @event.NetworkId,
            @event.Name,
            @event.Age
        });
    }

    public async Task Handle(SimNeedsChanged @event)
    {
        await hubContext.Clients.Group($"sim:{@event.NetworkId}").SendAsync("SimNeedsChanged", new
        {
            @event.NetworkId,
            @event.Hunger,
            @event.Energy,
            @event.Hygiene,
            @event.Bladder
        });
    }

    public async Task Handle(SimActionStarted @event)
    {
        await hubContext.Clients.Group($"sim:{@event.NetworkId}").SendAsync("SimActionStarted", new
        {
            @event.NetworkId,
            @event.ActionType,
            @event.Duration
        });
    }

    public async Task Handle(SimActionCompleted @event)
    {
        await hubContext.Clients.Group($"sim:{@event.NetworkId}").SendAsync("SimActionCompleted", new
        {
            @event.NetworkId,
            @event.ActionType
        });
    }
}