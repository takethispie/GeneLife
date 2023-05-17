using Arch.Bus;
using GeneLife.Common.Data;

namespace GeneLife.Common.Systems;

public delegate void LogNotification();

public partial class LogSystem
{
    private readonly bool _logToConsole;
    public List<string> Logs { get; init; }
    public event LogNotification LogAdded;

    public LogSystem(bool logToConsole)
    {
        _logToConsole = logToConsole;
        Logs = new List<string>();
        Hook();
    }
    
    [Event]
    public void Log(LogEvent log)
    {
        if(_logToConsole) Console.WriteLine(log.Message);
        else
        {
            Logs.Add(log.Message);
            OnLogNotification();
        }
    }
    
    protected virtual void OnLogNotification() //protected virtual method
    {
        //if ProcessCompleted is not null then call delegate
        LogAdded?.Invoke(); 
    }
}