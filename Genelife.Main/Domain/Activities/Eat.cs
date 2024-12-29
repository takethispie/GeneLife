using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.Main.Domain.Activities;

public class Eat : ILivingActivity {
    
    public int TickDuration { get; set; } = Constants.TickPerHour;

    public Human Apply(Human being) => being with { Hunger = 100 };
}