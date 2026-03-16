using Genelife.Messages.Events.Human;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Hubs;

public class HumanHub : Hub
{
    /// <summary>
    /// Clients can call this to subscribe to updates for a specific human by their correlation ID.
    /// </summary>
    public async Task SubscribeToHuman(string humanId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, humanId);
    }

    /// <summary>
    /// Clients can call this to unsubscribe from updates for a specific human.
    /// </summary>
    public async Task UnsubscribeFromHuman(string humanId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, humanId);
    }
}
