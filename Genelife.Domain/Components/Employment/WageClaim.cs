using Arch.Core;
using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// An unpaid claim held by a worker (or former worker) against a company.
/// Claims are paid oldest-first and survive resignation.
/// </summary>
public record struct WageClaim
{
    public Entity Company;
    public Money UnpaidGross;
    public Money UnpaidNet;
    public Money Severance;
    public int CyclesInArrears;
    public long FirstMissedTick;

    public WageClaim(Entity company, Money unpaidGross, Money unpaidNet, long tick)
    {
        Company = company;
        UnpaidGross = unpaidGross;
        UnpaidNet = unpaidNet;
        Severance = Money.Zero;
        CyclesInArrears = 1;
        FirstMissedTick = tick;
    }
}
