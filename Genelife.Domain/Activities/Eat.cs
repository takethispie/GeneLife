using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Eat : IBeingActivity {
    
    public int TickDuration { get; set; } = IBeingActivity.TickPerHour;
}