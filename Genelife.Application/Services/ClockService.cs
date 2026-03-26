using System.Timers;
using Genelife.Messages.Events.Clock;
using MassTransit;

namespace Genelife.Application.Services;

public class ClockService {
    private readonly IServiceProvider services;
    private readonly System.Timers.Timer timer;
    private DateTime dateTime;

    public ClockService(IServiceProvider services) {
        this.services = services;
        timer = new(1000);
        timer.Elapsed += OnTimedEvent!;
        timer.AutoReset = true;
        dateTime = new DateTime(100, 1, 1, 0, 0, 0);
    }

    public void Start() {
        timer.Enabled = true;
    }

    public void Stop() {
        timer.Enabled = false;
    }   

    private async void OnTimedEvent(object source, ElapsedEventArgs e) {
        using var scope = services.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var previousDate = dateTime;
        dateTime = dateTime.AddMinutes(15);
        await publishEndpoint.Publish(new Tick(dateTime));
        if(previousDate.AddHours(1) < dateTime) return;
        await publishEndpoint.Publish(new HourElapsed(new TimeOnly(dateTime.Hour, dateTime.Minute, dateTime.Second)));
        switch (dateTime.Hour) {
            case 12:
                Console.WriteLine($"Noon of current day: {dateTime}");
                break;
            case 0:
                Console.WriteLine($"new day: {dateTime}");
                await publishEndpoint.Publish(new DayElapsed(new DateTime(dateTime.Year, dateTime.Month, dateTime.Day)));
                break;
        }
    }

    public void SetSpeed(int milliseconds) {
        timer.Interval = milliseconds >= 100 ? milliseconds : 1000;
    }
}