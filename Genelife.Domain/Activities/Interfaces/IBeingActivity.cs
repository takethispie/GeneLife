using Genelife.Domain.Human;

namespace Genelife.Domain.Activities.Interfaces;

public interface IBeingActivity {
    public int TickDuration { get; set; }
    public static int TickPerHour => 4;
}