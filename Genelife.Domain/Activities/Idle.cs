using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Activities;

public class Idle : ILivingActivity {
    
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour;
    public bool GoHomeWhenFinished => false;

    public Human Apply(Human being) => being;
}