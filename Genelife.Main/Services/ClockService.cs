using System.Timers;
using Genelife.Domain;
using Genelife.Domain.Events;
using Genelife.Domain.Events.Clock;
using MassTransit;

namespace Genelife.Main.Services;

public class ClockService {
    private System.Timers.Timer timer;
    private IPublishEndpoint publishEndpoint;
    private int ticks;
    private TimeOnly timeOnly;

    public ClockService(IPublishEndpoint endpoint) {
        publishEndpoint = endpoint;
        timer = new(1000);
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = true;
        ticks = 0;
        timeOnly = new(0, 0);
    }

    public void Start() {
        timer.Enabled = true;
    }

    public void Stop() {
        timer.Enabled = false;
    }   

    private async void OnTimedEvent(object source, ElapsedEventArgs e)
    {
        await publishEndpoint.Publish(new Tick());
        ticks++;
        if(ticks < Constants.TickPerHour) return;
        ticks = 0;
        await publishEndpoint.Publish(new HourElapsed());
        timeOnly = timeOnly.AddHours(1);
        switch (timeOnly.Hour) {
            case 12:
                Console.WriteLine("Noon of current day");
                break;
            case 0:
                Console.WriteLine("1 day went by");
                await publishEndpoint.Publish(new DayElapsed());
                break;
        }
    }

    public void SetSpeed(int milliseconds) {
        timer.Interval = milliseconds >= 100 ? milliseconds : 1000;
    }
}