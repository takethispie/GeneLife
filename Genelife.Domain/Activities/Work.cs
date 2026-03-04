using Genelife.Domain.Interfaces;

namespace Genelife.Domain.Activities;

public class Work : ILivingActivity {
    
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour * 6;
    public bool GoHomeWhenFinished => true;

    public Human Apply(Human being) => being with {
        Energy = Math.Clamp(being.Energy - 40, 0, 100),
    };
}