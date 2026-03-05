using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Idle : IBeingActivity {
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
}