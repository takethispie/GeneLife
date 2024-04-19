using Genelife.Domain.Commands;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class HumanCreationConsumer(HumanRepository humanRepository) : IConsumer<CreateHuman>
{
    private HumanRepository repository = humanRepository;

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"storing position of human: {context.Message.CorrelationId}");
        var msg = context.Message;
        repository.Add(new Human(msg.CorrelationId, msg.Position));
        return Task.CompletedTask;
    }
}