using System.Timers;
using Genelife.Domain.Events;
using MassTransit;

namespace Genelife.Main.Services;

public class ClockService {
    private System.Timers.Timer Timer;
    private IPublishEndpoint PublishEndpoint;
    private int Ticks;
    private TimeOnly TimeOnly;

    public ClockService(IPublishEndpoint endpoint) {
        PublishEndpoint = endpoint;
        Timer = new System.Timers.Timer(1000);
        Timer.Elapsed += OnTimedEvent;
        Timer.AutoReset = true;
        Ticks = 0;
        TimeOnly = new TimeOnly(0, 0);
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
        Console.WriteLine("1 hour went by");
        await PublishEndpoint.Publish(new HourElapsed());
        TimeOnly = TimeOnly.AddHours(1);
        if(TimeOnly.Hour == 0) {
            Console.WriteLine("1 day went by");
            await PublishEndpoint.Publish(new DayElapsed());
        }
    }

    public void SetSpeed(int milliseconds) {
        if(milliseconds >= 200) Timer.Interval = milliseconds;
        else Timer.Interval = 1000;
    }
}