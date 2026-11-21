namespace Genelife.Components.Gen;

/// <summary>
/// Component representing an entity's money/balance
/// </summary>
public record struct Wallet
{
    public float Balance;
    
    public Wallet(float balance = 0f)
    {
        Balance = balance;
    }
}
