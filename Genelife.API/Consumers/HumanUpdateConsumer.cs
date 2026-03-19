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
            .SendAsync("HumanUpdate", update, context.CancellationToken);

        await hubContext.Clients
            .Group("all-humans")
            .SendAsync("HumanUpdate", update, context.CancellationToken);
    }
}
