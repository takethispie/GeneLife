using Genelife.Application.Services;
using Genelife.Messages.Commands.Clock;
using MassTransit;
using Serilog;

namespace Genelife.Application.Consumers;

public class ClockConsumer(ClockService service) : IConsumer<StartClock>, IConsumer<SetClockSpeed>, IConsumer<StopClock>
{
    public Task Consume(ConsumeContext<StartClock> context)
    {
        Log.Information("Started simulation");
        service.Start();
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<SetClockSpeed> context)
    {
        Log.Information("Set clock speed to {MessageMilliseconds} ms", context.Message.Milliseconds);
        service.SetSpeed(context.Message.Milliseconds);
        return Task.CompletedTask;
    }

    public Task Consume(ConsumeContext<StopClock> context)
    {
        Log.Information("Stopped simulation");
        service.Stop();
        return Task.CompletedTask;
    }
}
