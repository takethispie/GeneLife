using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Housing;

/// <summary>
/// In-flight residential construction. A developer of <see cref="Entity.Null"/>
/// is the municipality building shelter capacity.
/// </summary>
public record struct ConstructionProject
{
    public ulong ProjectId;
    public Entity Developer;
    public string District;
    public HousingTier Tier;
    public int Units;
    public Money Cost;
    public Money PaidToDate;
    public int MonthsRemaining;
    public int TotalMonths;
    public long StartedAtTick;

    public Money PaymentPerMonth =>
        TotalMonths <= 0 ? Cost : new Money((long)Math.Ceiling(Cost.Cents / (decimal)TotalMonths));
}
