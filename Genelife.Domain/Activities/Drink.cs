using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Drink : IBeingActivity {
    
    public int TickDuration { get; set; } = IBeingActivity.TickPerHour;
}