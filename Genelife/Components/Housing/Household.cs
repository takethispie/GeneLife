using Arch.Core;
using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Components.Housing;

/// <summary>
/// The cost-sharing unit. The household is the source of truth for tenancy,
/// rent, arrears and the eviction ladder; <see cref="Residence"/> is a derived
/// per-sim cache.
/// </summary>
public record struct Household
{
    public Entity Head;
    public Entity Unit;
    public Tenure Tenure;
    public Money MonthlyRent;
    public long MoveInTick;
    public int ArrearsCycles;
    public Money RentArrears;
    public Money UtilityArrears;
    public Money Debt;
    public long LastRentTick;
    public long LastUtilityTick;
    public int NoticeStage;
    public long NoticeStartedTick;
    public long CureDeadlineTick;
    public long LastMoveTick;
    public int OverburdenMonths;
    public bool NoUtilities;
    public int MemberCount;
}
