using Arch.Bus;
using GeneLife.Common.Data;
using GeneLife.Core.Events;

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
            if (string.IsNullOrEmpty(log.Message)) return;
            Logs.Add(log.Message);
            OnLogNotification();
        }
    }
    
    protected virtual void OnLogNotification()
    {
        LogAdded?.Invoke(); 
    }
}