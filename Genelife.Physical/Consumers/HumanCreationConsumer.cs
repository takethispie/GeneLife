using System.Numerics;
using Genelife.Domain.Commands;
using Genelife.Physical.Domain;
using Genelife.Physical.Repository;
using MassTransit;

namespace Genelife.Physical.Consumers;

public class HumanCreationConsumer(HumanCache humanRepository) : IConsumer<CreateHuman>
{
    private HumanCache repository = humanRepository;

    public Task Consume(ConsumeContext<CreateHuman> context)
    {
        Console.WriteLine($"storing position of human: {context.Message.CorrelationId}");
        var msg = context.Message;
        repository.Add(new Human(msg.CorrelationId, new Vector3(msg.X, msg.Y, 0)));
        return Task.CompletedTask;
    }
}