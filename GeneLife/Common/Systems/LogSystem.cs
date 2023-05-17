using Arch.Bus;
using GeneLife.Common.Data;

namespace GeneLife.Common.Systems;

public partial class LogSystem
{
    public LogSystem()
    {
        Hook();
    }
    
    [Event]
    public void Log(LogEvent log) => Console.WriteLine(log.Message);
}