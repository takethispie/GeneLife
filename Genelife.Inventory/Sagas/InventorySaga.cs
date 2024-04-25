using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Inventory.Sagas;

public class InventorySaga : ISaga, InitiatedBy<CreateHuman>
{
    public Guid CorrelationId { get; set; }

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s thirst set to 0");
        return Task.CompletedTask;
    }
}