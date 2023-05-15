using Arch.Bus;
using Arch.EventBus;
using GeneLife.Data;

namespace GeneLife.Systems;

public partial class LogSystem
{
    [Event]
    public void Log(LogEvent log) => Console.WriteLine(log.Message);
}