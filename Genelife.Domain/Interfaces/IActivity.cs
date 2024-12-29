using OneOf;

namespace Genelife.Domain.Interfaces;

public interface IActivity {
    int TickDuration { get; set; }

    Human Apply(Human being);
}