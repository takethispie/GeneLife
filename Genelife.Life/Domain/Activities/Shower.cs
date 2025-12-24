using Genelife.Life.Interfaces;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Domain.Activities;

public class Shower : ILivingActivity {
    public int TickDuration { get; set; } = 5;
    public bool GoHomeWhenFinished => false;
    
    public Human Apply(Human being) => being with { Hygiene = 100 };
}