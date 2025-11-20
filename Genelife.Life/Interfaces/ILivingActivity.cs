using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Interfaces;

public interface ILivingActivity {
    public int TickDuration { get; set; }

    public Human Apply(Human being);
}