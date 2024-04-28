using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class HungerSaga : ISaga, InitiatedBy<CreateHuman>, Observes<DayElapsed, HungerSaga>, Orchestrates<Eat>, Orchestrates<SetHunger>, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int Hunger { get; set; }
    public int Version { get; set; }

    public Expression<Func<HungerSaga, DayElapsed, bool>> CorrelationExpression => (_, _) => true;

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s hunger set to 0");
        Hunger = 0;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        Hunger++;
        if (Hunger >= 10)
        {
            Console.WriteLine($"{CorrelationId} is starving");
            await context.Publish(new Starving(CorrelationId));
        }
        else if (Hunger >= 8)
        {
            Console.WriteLine($"{CorrelationId} started being hungry");
            await context.Publish(new StartedBeingHungry(CorrelationId));
        }
        Console.WriteLine($"{CorrelationId} Hunger level: {Hunger}");
        return;
    }

    public async Task Consume(ConsumeContext<Eat> context)
    {
        Hunger = 0;
        Console.WriteLine($"human {CorrelationId} ate food");
        await context.Publish(new HasEaten(CorrelationId));
    }

    public Task Consume(ConsumeContext<SetHunger> context)
    {
        Hunger = context.Message.Value;
        Console.WriteLine($"set Hunger to {Hunger} for human {CorrelationId}");
        return Task.CompletedTask;
    }
}