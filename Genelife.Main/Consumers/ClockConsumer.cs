using Genelife.Domain.Commands;
using Genelife.Main.Services;
using MassTransit;
using Serilog;

namespace Genelife.Main.Consumers;

public class ClockConsumer(ClockService service) : IConsumer<StartClock>, IConsumer<SetClockSpeed>, IConsumer<StopClock>
{
    private readonly ClockService ClockService = service;

    public Task Consume(ConsumeContext<StartClock> context)
    {
        Log.Information("Started simulation");
        ClockService.Start();
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<SetClockSpeed> context)
    {
        Log.Information($"Set clock speed to {context.Message.Milliseconds} ms");
        ClockService.SetSpeed(context.Message.Milliseconds);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<StopClock> context)
    {
        Log.Information("Stopped simulation");
        ClockService.Stop();
        return Task.CompletedTask;
    }
}
