using Genelife.API.Constants;
using Genelife.API.Hubs;
using Genelife.Application.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.API.Consumers;

public class CompanyUpdateConsumer(IHubContext<CompanyHub> hubContext) : IConsumer<CompanyUpdate>
{
    public async Task Consume(ConsumeContext<CompanyUpdate> context)
    {
        var update = context.Message;

        await hubContext.Clients
            .Group(update.CorrelationId.ToString())
            .SendAsync(SignalRMethods.CompanyUpdate, update, context.CancellationToken);

        await hubContext.Clients
            .Group(SignalRGroups.AllCompanies)
            .SendAsync(SignalRMethods.CompanyUpdate, update, context.CancellationToken);
    }
}
