using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Interfaces;

public interface ILivingActivity {
    public int TickDuration { get; set; }
    public static int TickPerHour => 10;

    public Human Apply(Human being);
}