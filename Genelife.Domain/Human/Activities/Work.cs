using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Human.Activities;

public class Work : IBeingActivity {
    
    public int TickDuration { get; set; } = IBeingActivity.TickPerHour * 6;
}