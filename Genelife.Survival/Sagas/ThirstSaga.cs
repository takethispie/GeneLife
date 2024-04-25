using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class ThirstSaga : ISaga, InitiatedBy<CreateHuman>, Observes<DayElapsed, ThirstSaga>, Orchestrates<Drink>, Orchestrates<SetThirst>
{
    public Guid CorrelationId { get; set; }
    public int Thirst { get; set; }

    public Expression<Func<ThirstSaga, DayElapsed, bool>> CorrelationExpression => (_, _) => true;

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"{context.Message.Human.FirstName} {context.Message.Human.LastName}'s thirst set to 0");
        Thirst = 0;
        return Task.CompletedTask;
    }

    public async Task Consume(ConsumeContext<DayElapsed> context)
    {
        Thirst++;
        if(Thirst >= 20) {
            Console.WriteLine($"{CorrelationId} is dehydrated");
            await context.Publish(new Dehydrated(CorrelationId));
        }
        else if(Thirst >= 17) {
            Console.WriteLine($"{CorrelationId} started being thirsty");
            await context.Publish(new StartedBeingThirsty(CorrelationId));
        }
        Console.WriteLine($"{CorrelationId} Thirst level: {Thirst}");
        return;
    }

    public async Task Consume(ConsumeContext<Drink> context)
    {
        Thirst = 0;
        Console.WriteLine($"human {CorrelationId} drank");
        await context.Publish(new HasDrank(CorrelationId));
    }

    public Task Consume(ConsumeContext<SetThirst> context)
    {
        Thirst = context.Message.Value;
        Console.WriteLine($"set thirst to {Thirst} for human {CorrelationId}");
        return Task.CompletedTask;
    }
}