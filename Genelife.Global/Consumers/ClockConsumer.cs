using Genelife.Global.Messages.Commands.Clock;
using Genelife.Global.Services;
using MassTransit;
using Serilog;

namespace Genelife.Global.Consumers;

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
        Log.Information($"Set clock speed to {context.Message.Milliseconds} ms");
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
