using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Hubs;

public class CompanyHub : Hub
{
    /// <summary>
    /// Clients call this to subscribe to updates for a specific company by its correlation ID.
    /// </summary>
    public async Task SubscribeToCompany(string companyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, companyId);
    }

    /// <summary>
    /// Clients call this to unsubscribe from a specific company's updates.
    /// </summary>
    public async Task UnsubscribeFromCompany(string companyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, companyId);
    }
}
