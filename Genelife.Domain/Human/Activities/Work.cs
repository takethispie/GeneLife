using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Human.Activities;

public class Work : IBeingActivity {
    
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(6);
}