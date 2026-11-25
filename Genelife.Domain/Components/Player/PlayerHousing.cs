using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Player;

/// <summary>
/// Player lease state. The household owns the rent obligation; this component
/// records the player-facing lease view and the accepted offer.
/// </summary>
public record struct PlayerHousing
{
    public Entity Household;
    public Entity Unit;
    public long LeaseStartTick;
    public Money Deposit;
    public bool LeaseActive;

    public PlayerHousing(Entity household, Entity unit, long leaseStartTick, Money deposit)
    {
        Household = household;
        Unit = unit;
        LeaseStartTick = leaseStartTick;
        Deposit = deposit;
        LeaseActive = true;
    }
}

/// <summary>
/// An expiring rental offer produced by <c>RequestHousing</c>. The offer does
/// not reserve the unit; accepting re-validates availability and capacity.
/// </summary>
public record struct PlayerLeaseOffer
{
    public Guid OfferId;
    public Entity Unit;
    public Entity Property;
    public Money MonthlyRent;
    public long CreatedTick;
    public long ExpiresTick;
}
