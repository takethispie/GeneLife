using Arch.Bus;
using GeneLife.Core.Events;

namespace GeneLife.Core;

public delegate void LogNotification();

public partial class LogSystem
{
    private readonly bool _logToConsole;
    private readonly int _maxLogLines;
    public List<string> Logs { get; init; }
    public event LogNotification LogAdded;

    public LogSystem(bool logToConsole, int maxLogLines = 25)
    {
        _logToConsole = logToConsole;
        _maxLogLines = maxLogLines;
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
            if(Logs.Count > _maxLogLines) Logs.RemoveAt(0);
            OnLogNotification();
        }
    }
    
    protected virtual void OnLogNotification()
    {
        LogAdded?.Invoke(); 
    }
}