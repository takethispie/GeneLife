using Genelife.API.Hubs;
using Genelife.Application.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Consumers;

public class GroceryUpdateConsumer(IHubContext<GroceryHub> hubContext) : IConsumer<GroceryUpdate>
{
    public async Task Consume(ConsumeContext<GroceryUpdate> context)
    {
        var update = context.Message;

        await hubContext.Clients
            .Group(update.CorrelationId.ToString())
            .SendAsync("GroceryUpdate", update, context.CancellationToken);

        await hubContext.Clients
            .Group("all-grocery-stores")
            .SendAsync("GroceryUpdate", update, context.CancellationToken);
    }
}
