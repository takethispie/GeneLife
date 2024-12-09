
namespace Genelife.Domain.Scheduler;

public record FreeSlot() : ISlot
{
    public TimeOnly Start { get; set; }
}
