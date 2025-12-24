using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Domain.Activities;

public class Eat : ILivingActivity {
    
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour;
    public bool GoHomeWhenFinished => false;

    public Human Apply(Human being) => being with { Hunger = 100 };
}