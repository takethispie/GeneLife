using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Family;

/// <summary>Explicit state-care record for a minor without a guardian.</summary>
public record struct StateCare
{
    public long SinceTick;
    public Entity CaseWorker;
    public Money MonthlyCost;
}
