using Genelife.Domain.Interfaces;

namespace Genelife.Domain.Activities;

public class Drink : ILivingActivity {
    
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour;
    public bool GoHomeWhenFinished => false;

    public Human Apply(Human being) => being with { Thirst = 100 };
}