using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class ThirstSaga : ISaga, InitiatedBy<CreateHuman>, IConsumer<HourElapsed>, IConsumer<DayElapsed>
{
    public Guid CorrelationId { get; set; }
    public int Thirst { get; set; }

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s thirst set to 0");
        Thirst = 0;
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<HourElapsed> context)
    {
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        Thirst++;
        if(Thirst >= 8) await context.Publish(new StartedBeingThirsty(CorrelationId));
        return;
    }
}