using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Domain.Activities;

public class Work : ILivingActivity {
    
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour * 6;

    public Human Apply(Human being) => being with {
        Energy = Math.Clamp(being.Energy - 40, 0, 100),
    };
}