using Genelife.Values;

namespace Genelife.Components.Employment;

/// <summary>
/// Unemployment claim state. Benefits are funded by the government account and
/// stop exactly on hire, retirement, death or exhaustion.
/// </summary>
public record struct UnemploymentRecord
{
    public long StartedTick;
    public Money BenefitPerCycle;
    public int CyclesClaimed;
    public int MaxCycles;
    public bool Eligible;
    public long LastPaidTick;
    public int UnpaidCycles;

    public UnemploymentRecord(long startedTick, Money benefitPerCycle, int maxCycles, bool eligible)
    {
        StartedTick = startedTick;
        BenefitPerCycle = benefitPerCycle;
        CyclesClaimed = 0;
        MaxCycles = maxCycles;
        Eligible = eligible;
        LastPaidTick = 0;
        UnpaidCycles = 0;
    }

    public bool IsExhausted => CyclesClaimed >= MaxCycles;
}
