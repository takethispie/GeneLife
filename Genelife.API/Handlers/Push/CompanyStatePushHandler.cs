using Genelife.Api.Hubs;
using Genelife.Messages.Events;
using Microsoft.AspNetCore.SignalR;

namespace Genelife.Api.Handlers.Push;

public class CompanyStatePushHandler
{
    private readonly IHubContext<WorldHub> hubContext;

    public CompanyStatePushHandler(IHubContext<WorldHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public async Task Handle(CompanyCreated @event)
    {
        await hubContext.Clients.Group($"company:{@event.NetworkId}").SendAsync("CompanyCreated", new
        {
            @event.NetworkId,
            @event.Name
        });
    }

    public async Task Handle(EmployeeHired @event)
    {
        await hubContext.Clients.Group($"company:{@event.CompanyId}").SendAsync("EmployeeHired", new
        {
            @event.CompanyId,
            @event.GenosId,
            @event.Position,
            @event.SalaryCents
        });
    }

    public async Task Handle(EmployeeFired @event)
    {
        await hubContext.Clients.Group($"company:{@event.CompanyId}").SendAsync("EmployeeFired", new
        {
            @event.CompanyId,
            @event.GenosId
        });
    }

    public async Task Handle(CompanyDeleted @event)
    {
        await hubContext.Clients.Group($"company:{@event.NetworkId}").SendAsync("CompanyDeleted", new
        {
            @event.NetworkId
        });
    }
}