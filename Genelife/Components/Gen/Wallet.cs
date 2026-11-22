using Genelife.Values;

namespace Genelife.Components.Gen;

/// <summary>
/// Component representing an entity's spendable cash.
/// </summary>
public record struct Wallet
{
    public Money Balance;

    public Wallet(Money balance)
    {
        Balance = balance;
    }

    public Wallet(decimal units) : this(Money.FromUnits(units))
    {
    }
}
