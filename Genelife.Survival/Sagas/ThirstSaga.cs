using System.Linq.Expressions;
using Genelife.Domain.Commands;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Survival.Sagas;

public class ThirstSaga : ISaga, InitiatedBy<CreateHuman>, Observes<DayElapsed, ThirstSaga>, Orchestrates<HasDrank>
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
        if(Thirst >= 20) {
            Console.WriteLine($"{CorrelationId} is starving");
            return;
        }
        Thirst++;
        if(Thirst >= 17) {
            Console.WriteLine($"{CorrelationId} started being thirsty");
            await context.Publish(new StartedBeingThirsty(CorrelationId));
        }
        Console.WriteLine($"{CorrelationId} Thirst level: {Thirst}");
        return;
    }

    public Task Consume(ConsumeContext<HasDrank> context)
    {
        Thirst = 0;
        return Task.CompletedTask;
    }
}