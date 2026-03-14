using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Eat(DateTime start) : IBeingActivity {
    public DateTime StartTime { get; } = start;
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);
}