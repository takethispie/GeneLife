using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Hubs;

public class CompanyHub : Hub
{
    public async Task SubscribeToCompany(string companyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, companyId);
    }

    public async Task UnsubscribeFromCompany(string companyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, companyId);
    }
}
