using Genelife.API.Hubs;
using Genelife.Application.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Consumers;

public class HumanUpdateConsumer(IHubContext<HumanHub> hubContext) : IConsumer<HumanUpdate>
{
    public async Task Consume(ConsumeContext<HumanUpdate> context)
    {
        var update = context.Message;

        // Broadcast to all clients subscribed to this specific human's group
        await hubContext.Clients
            .Group(update.CorrelationId.ToString())
            .SendAsync("HumanUpdate", update, context.CancellationToken);

        // Also broadcast to the "all-humans" group for clients that want every update
        await hubContext.Clients
            .Group("all-humans")
            .SendAsync("HumanUpdate", update, context.CancellationToken);
    }
}
