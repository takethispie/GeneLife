using Genelife.Api.DTOs;
using Genelife.Api.Hubs;
using Genelife.Messages.Events;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Handlers.Push;

public class GenosStatePushHandler
{
    private readonly IHubContext<WorldHub> hubContext;

    public GenosStatePushHandler(IHubContext<WorldHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public async Task Handle(GenosCreated @event)
    {
        await hubContext.Clients.Group("all-genoses").SendAsync("GenosCreated", new
        {
            @event.NetworkId,
            @event.Name,
            @event.Age
        });
    }

    public async Task Handle(GenosNeedsChanged @event)
    {
        await hubContext.Clients.Group($"genos:{@event.NetworkId}").SendAsync("GenosNeedsChanged", new
        {
            @event.NetworkId,
            @event.Hunger,
            @event.Energy,
            @event.Hygiene,
            @event.Bladder
        });
    }

    public async Task Handle(GenosActionStarted @event)
    {
        await hubContext.Clients.Group($"genos:{@event.NetworkId}").SendAsync("GenosActionStarted", new
        {
            @event.NetworkId,
            @event.ActionType,
            @event.Duration
        });
    }

    public async Task Handle(GenosActionCompleted @event)
    {
        await hubContext.Clients.Group($"genos:{@event.NetworkId}").SendAsync("GenosActionCompleted", new
        {
            @event.NetworkId,
            @event.ActionType
        });
    }
}