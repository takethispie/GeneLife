using Genelife.Domain.Activities.Interfaces;

namespace Genelife.Domain.Activities;

public class Sleep : ILivingActivity {
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour * 8;
    public bool GoHomeWhenFinished => false;

    public Human Apply(Human being) => being with { Energy = 100 };
}