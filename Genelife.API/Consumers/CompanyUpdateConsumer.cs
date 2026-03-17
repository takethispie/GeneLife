using Genelife.API.Hubs;
using Genelife.Application.IntegrationEvents;
using Genelife.Messages.Events.Company;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Consumers;

public class CompanyUpdateConsumer(IHubContext<CompanyHub> hubContext) : IConsumer<CompanyUpdate>
{
    public async Task Consume(ConsumeContext<CompanyUpdate> context)
    {
        var update = context.Message;

        // Broadcast to clients subscribed to this specific company
        await hubContext.Clients
            .Group(update.CorrelationId.ToString())
            .SendAsync("CompanyUpdate", update, context.CancellationToken);

        // Broadcast to the "all-companies" group for the list page
        await hubContext.Clients
            .Group("all-companies")
            .SendAsync("CompanyUpdate", update, context.CancellationToken);
    }
}
