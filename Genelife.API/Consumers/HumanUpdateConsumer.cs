using Genelife.API.Constants;
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

        await hubContext.Clients
            .Group(update.CorrelationId.ToString())
            .SendAsync(SignalRMethods.HumanUpdate, update, context.CancellationToken);

        await hubContext.Clients
            .Group(SignalRGroups.AllHuman)
            .SendAsync(SignalRMethods.HumanUpdate, update, context.CancellationToken);
    }
}
