using System.Timers;
using Genelife.Domain.Events;
using Genelife.Domain.Events.Clock;
using MassTransit;

namespace Genelife.Main.Services;

public class ClockService {
    private System.Timers.Timer Timer;
    private IPublishEndpoint PublishEndpoint;
    private int Ticks;
    private TimeOnly TimeOnly;

    public ClockService(IPublishEndpoint endpoint) {
        PublishEndpoint = endpoint;
        Timer = new(1000);
        Timer.Elapsed += OnTimedEvent;
        Timer.AutoReset = true;
        Ticks = 0;
        TimeOnly = new(0, 0);
    }

    public void Start() {
        Timer.Enabled = true;
    }

    public void Stop() {
        Timer.Enabled = false;
    }   

    private async void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
        await PublishEndpoint.Publish(new Tick());
        Ticks++;
        if(Ticks < 10) return;
        Ticks = 0;
        await PublishEndpoint.Publish(new HourElapsed());
        TimeOnly = TimeOnly.AddHours(1);
        if(TimeOnly.Hour == 12) Console.WriteLine("Noon of current day");
        if(TimeOnly.Hour == 0) {
            Console.WriteLine("1 day went by");
            await PublishEndpoint.Publish(new DayElapsed());
        }
    }

    public void SetSpeed(int milliseconds) {
        if(milliseconds >= 100) Timer.Interval = milliseconds;
        else Timer.Interval = 1000;
    }
}