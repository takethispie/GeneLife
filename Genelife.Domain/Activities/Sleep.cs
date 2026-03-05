using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Sleep : IBeingActivity {
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(8);
}