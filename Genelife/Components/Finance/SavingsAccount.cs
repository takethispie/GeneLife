using Genelife.Values;

namespace Genelife.Components.Finance;

/// <summary>
/// Household savings tier. Savings are tapped only after spendable cash and are
/// never allowed to go negative.
/// </summary>
public record struct SavingsAccount
{
    public Money Balance;
    public Money LifetimeDeposited;
    public Money LifetimeWithdrawn;
    public Money InterestEarned;
    public long LastInterestTick;

    public SavingsAccount(Money balance = default)
    {
        Balance = balance;
        LifetimeDeposited = Money.Zero;
        LifetimeWithdrawn = Money.Zero;
        InterestEarned = Money.Zero;
        LastInterestTick = 0;
    }
}
