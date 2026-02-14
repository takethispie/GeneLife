using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Interfaces;

public interface ILivingActivity {
    public int TickDuration { get; set; }
    public static int TickPerHour => 4;
    public bool GoHomeWhenFinished { get; }

    public Human Apply(Human being);
}