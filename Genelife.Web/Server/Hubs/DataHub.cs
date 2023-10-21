using Genelife.Web.Shared;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Web.Server.Hubs;

public class DataHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync(MessageType.SimulationTick, user, message);
    }

    public async Task SendLog(string log)
    {
        await Clients.All.SendAsync(MessageType.SimulationLog, log);
    }
}