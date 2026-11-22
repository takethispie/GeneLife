using Genelife.Values;

namespace Genelife.Life;

/// <summary>
/// Population inflow policy. Inflow is disabled until a non-zero target is set,
/// so existing worlds do not start spawning immigrants unexpectedly.
/// </summary>
public sealed class ImmigrationPolicy
{
    public int TargetPopulation { get; set; }
    public bool Enabled { get; set; } = true;
    public decimal MinimumWorkingAgeShare { get; set; } = 0.55m;
    public decimal UnemploymentPauseThreshold { get; set; } = 0.10m;
    public Money ImmigrantStartingCash { get; set; } = Money.FromUnits(500m);
    public decimal AnnualArrivalShare { get; set; } = 0.02m;
    public int MinimumAnnualArrivals { get; set; } = 5;
    public int PendingExpiryMonths { get; set; } = 12;
    public int MinArrivalAge { get; set; } = 18;
    public int MaxArrivalAge { get; set; } = 45;

    public static ImmigrationPolicy Default { get; } = new();

    public ImmigrationPolicy Clone() => new()
    {
        TargetPopulation = TargetPopulation,
        Enabled = Enabled,
        MinimumWorkingAgeShare = MinimumWorkingAgeShare,
        UnemploymentPauseThreshold = UnemploymentPauseThreshold,
        ImmigrantStartingCash = ImmigrantStartingCash,
        AnnualArrivalShare = AnnualArrivalShare,
        MinimumAnnualArrivals = MinimumAnnualArrivals,
        PendingExpiryMonths = PendingExpiryMonths,
        MinArrivalAge = MinArrivalAge,
        MaxArrivalAge = MaxArrivalAge
    };

    public int AnnualBudget(int currentPopulation)
    {
        if (currentPopulation <= 0)
            return 0;
        var share = (int)Math.Ceiling(currentPopulation * AnnualArrivalShare);
        return Math.Max(MinimumAnnualArrivals, share);
    }
}
