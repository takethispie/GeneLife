using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Hubs;

public class GroceryHub : Hub
{
    public async Task SubscribeToGroceryStore(string storeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, storeId);
    }

    public async Task UnsubscribeFromGroceryStore(string storeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, storeId);
    }
}
