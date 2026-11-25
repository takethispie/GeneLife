using Genelife.Values;

namespace Genelife.Economy;

/// <summary>
/// Reserved network ids for the public/fiscal accounts. They participate in the
/// ledger without being ECS entities so the tick loop can move money cheaply.
/// </summary>
public static class EconomyAccountIds
{
    public const ulong Government = ulong.MaxValue - 10;
    public const ulong PensionFund = ulong.MaxValue - 11;
    public const ulong Bank = ulong.MaxValue - 12;
}

public sealed class GovernmentAccount
{
    public ulong NetworkId { get; init; } = EconomyAccountIds.Government;
    public Money Balance { get; set; }
    public Money IncomeTaxCollected { get; set; }
    public Money CorporateTaxCollected { get; set; }
    public Money BenefitsPaid { get; set; }
    public Money OfficeCostsCollected { get; set; }

    public void Clear()
    {
        Balance = Money.Zero;
        IncomeTaxCollected = Money.Zero;
        CorporateTaxCollected = Money.Zero;
        BenefitsPaid = Money.Zero;
        OfficeCostsCollected = Money.Zero;
    }
}

public sealed class PensionFund
{
    public ulong NetworkId { get; init; } = EconomyAccountIds.PensionFund;
    public Money Balance { get; set; }
    public Money ContributionsThisCycle { get; set; }
    public Money PayoutsThisCycle { get; set; }
    public Money Shortfall { get; set; }
    public decimal TargetFundingRatio { get; set; } = 1.0m;

    public void Clear()
    {
        Balance = Money.Zero;
        ContributionsThisCycle = Money.Zero;
        PayoutsThisCycle = Money.Zero;
        Shortfall = Money.Zero;
    }
}

public sealed class Bank
{
    public ulong NetworkId { get; init; } = EconomyAccountIds.Bank;
    public Money Balance { get; set; }
    public Money InterestPool { get; set; }
    public decimal SavingsRate { get; set; }
    public decimal LendingRate { get; set; } = 0.08m;

    public void Clear()
    {
        Balance = Money.Zero;
        InterestPool = Money.Zero;
    }
}
