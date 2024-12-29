using Genelife.Domain;
using Genelife.Domain.Interfaces;
using OneOf;

namespace Genelife.Main.Domain.Activities;

public class Sleep : IActivity {
    public int TickDuration { get; set; } = Constants.TickPerHour * 8;

    public Human Apply(Human being) => being with { Energy = 100 };
}