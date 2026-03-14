using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Human.Activities;

public class Shower(DateTime start) : IBeingActivity {
    public DateTime StartTime { get; } = start;
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
}