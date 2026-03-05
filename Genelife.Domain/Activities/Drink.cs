using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Drink : IBeingActivity {
    
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);
}