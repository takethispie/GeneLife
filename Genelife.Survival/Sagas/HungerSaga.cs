using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class HungerSaga : ISaga, InitiatedBy<CreateHuman>, Observes<DayElapsed, HungerSaga>, Orchestrates<HasEaten>
{
    public Guid CorrelationId { get; set; }
    public int Hunger { get; set; }

    public Expression<Func<HungerSaga, DayElapsed, bool>> CorrelationExpression => (_,_) => true;

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s hunger set to 0");
        Hunger = 0;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        if(Hunger >= 10) {
            Console.WriteLine($"{CorrelationId} is starving");
            
            return;
        }
        Hunger++;
        if(Hunger >= 8) {
            Console.WriteLine($"{CorrelationId} started being hungry");
            await context.Publish(new StartedBeingHungry(CorrelationId));
        }
        Console.WriteLine($"{CorrelationId} Hunger level: {Hunger}");
        return;
    }

    public Task Consume(ConsumeContext<HasEaten> context)
    {
        Hunger = 0;
        return Task.CompletedTask;
    }
}