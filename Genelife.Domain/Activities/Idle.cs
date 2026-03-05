using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Idle : IBeingActivity {
    
    public int TickDuration { get; set; } = IBeingActivity.TickPerHour;

    public Person Apply(Person being) => being;
}