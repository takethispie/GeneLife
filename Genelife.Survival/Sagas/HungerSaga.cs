using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class HungerSaga : ISaga, InitiatedBy<CreateHuman>, IConsumer<HourElapsed>, IConsumer<DayElapsed>
{
    public Guid CorrelationId { get; set; }
    public int Hunger { get; set; }

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s hunger set to 0");
        Hunger = 0;
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<HourElapsed> context)
    {
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        Hunger++;
        if(Hunger >= 8) await context.Publish(new StartedBeingHungry(CorrelationId));
        return;
    }
}