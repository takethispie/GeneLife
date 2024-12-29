using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.Main.Domain.Activities;

public class Shower : IActivity {
    public int TickDuration { get; set; } = 5;
    
    public Human Apply(Human being) => being with { Hygiene = 100 };
}