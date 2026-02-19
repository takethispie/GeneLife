using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Domain.Activities;

public class Drink : ILivingActivity {
    
    public int TickDuration { get; set; } = ILivingActivity.TickPerHour;
    public bool GoHomeWhenFinished => false;

    public Human Apply(Human being) => being with { Thirst = 100 };
}