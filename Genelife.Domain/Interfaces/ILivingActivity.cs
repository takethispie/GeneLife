using OneOf;

namespace Genelife.Domain.Interfaces;

public interface IActivity {
    public int TickDuration { get; set; }

    public Human Apply(Human being);
}