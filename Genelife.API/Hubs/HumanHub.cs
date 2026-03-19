using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Hubs;

public class HumanHub : Hub
{
    public async Task SubscribeToHuman(string humanId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, humanId);
    }

    public async Task UnsubscribeFromHuman(string humanId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, humanId);
    }
}
