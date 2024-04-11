using System;
using System.Timers;

namespace Genelife.Web.Services;

public class ClockService
{
    private readonly Timer timer;
    private readonly Action<float> onClock;

    public ClockService(int interval, Action<float> onUpdate)
    {
        timer = new Timer(interval);
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = true;
        timer.Enabled = false;
        onClock = onUpdate;
    }

    private void OnTimedEvent(Object source, ElapsedEventArgs e) => onClock(1);

    public void Start() => timer.Enabled = true;

    public void SetInterval(int interval) => timer.Interval = interval;
}