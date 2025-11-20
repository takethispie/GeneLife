using Genelife.Global.Messages.Commands.Clock;
using Genelife.Life.Services;
using MassTransit;
using Serilog;

namespace Genelife.Life.Consumers;

public class ClockConsumer(ClockService service) : IConsumer<StartClock>, IConsumer<SetClockSpeed>, IConsumer<StopClock>
{
    private readonly ClockService clockService = service;

    public Task Consume(ConsumeContext<StartClock> context)
    {
        Log.Information("Started simulation");
        clockService.Start();
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<SetClockSpeed> context)
    {
        Log.Information($"Set clock speed to {context.Message.Milliseconds} ms");
        clockService.SetSpeed(context.Message.Milliseconds);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<StopClock> context)
    {
        Log.Information("Stopped simulation");
        clockService.Stop();
        return Task.CompletedTask;
    }
}
