using Genelife.Domain.Commands;
using Genelife.Main.Services;
using MassTransit;

namespace Genelife.Main.Consumers;

public class ClockConsumer(ClockService service) : IConsumer<StartClock>
{
    private readonly ClockService ClockService = service;

    public Task Consume(ConsumeContext<StartClock> context)
    {
        ClockService.Start();
        return Task.CompletedTask;
    }
}
