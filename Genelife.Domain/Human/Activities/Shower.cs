using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Human.Activities;

public class Shower : IBeingActivity {
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
}