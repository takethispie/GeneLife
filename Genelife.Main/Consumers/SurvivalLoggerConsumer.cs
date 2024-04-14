using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Main.Consumers;

public class SurvivalLoggerConsumer : IConsumer<StartedBeingHungry>, IConsumer<StartedBeingThirsty>
{
    public Task Consume(ConsumeContext<StartedBeingHungry> context)
    {
        Console.WriteLine($"{context.Message.CorrelationId} started being hungry");
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<StartedBeingThirsty> context)
    {
        Console.WriteLine($"{context.Message.CorrelationId} started being thirsty");
        return Task.CompletedTask;
    }
}
