using Genelife.API.Constants;
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
            .SendAsync(SignalRMethods.GroceryUpdate, update, context.CancellationToken);

        await hubContext.Clients
            .Group(SignalRGroups.AllGroceryStores)
            .SendAsync(SignalRMethods.GroceryUpdate, update, context.CancellationToken);
    }
}
