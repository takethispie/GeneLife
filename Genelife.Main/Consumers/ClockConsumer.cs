using Genelife.Domain.Commands;
using Genelife.Main.Services;
using MassTransit;

namespace Genelife.Main.Consumers;

public class ClockConsumer(ClockService service) : IConsumer<StartClock>, IConsumer<SetClockSpeed>, IConsumer<StopClock>
{
    private readonly ClockService ClockService = service;

    public Task Consume(ConsumeContext<StartClock> context)
    {
        Console.WriteLine("Started simulation");
        ClockService.Start();
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<SetClockSpeed> context)
    {
        Console.WriteLine($"Set clock speed to {context.Message.Milliseconds} ms");
        ClockService.SetSpeed(context.Message.Milliseconds);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<StopClock> context)
    {
        Console.WriteLine("Stopped simulation");
        ClockService.Stop();
        return Task.CompletedTask;
    }
}
