using OneOf;

namespace Genelife.Domain.Interfaces;

public interface ILivingActivity {
    public int TickDuration { get; set; }

    public Human Apply(Human being);
}