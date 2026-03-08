using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Human.Activities;

public class Work(DateTime start) : IBeingActivity {
    public DateTime StartTime { get; } = start;
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(6);
}