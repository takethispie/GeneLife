using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Human;

namespace Genelife.Domain.Activities;

public class Sleep : IBeingActivity {
    public int TickDuration { get; set; } = IBeingActivity.TickPerHour * 8;
}