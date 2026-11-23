using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Tunable housing policy. Defaults are deliberately conservative so a single
/// bad month does not evict a household and a stable market does not oscillate.
/// </summary>
public sealed class HousingOptions
{
    public decimal AffordabilityRatio { get; set; } = 0.35m;
    public decimal RentBurdenTrigger { get; set; } = 0.40m;
    public decimal TargetVacancyRate { get; set; } = 0.05m;
    public decimal VacancyResponse { get; set; } = 0.50m;
    public decimal MaxIndexStep { get; set; } = 0.05m;
    public decimal MinRentIndex { get; set; } = 0.7m;
    public decimal MaxRentIndex { get; set; } = 1.6m;
    public decimal AnnualRentAdjustmentCap { get; set; } = 0.10m;
    public int MaxEvictionsPerCycle { get; set; } = 5;
    public int NoticeMonths { get; set; } = 1;
    public int MoveCooldownMonths { get; set; } = 3;
    public int OverburdenMonthsBeforeMove { get; set; } = 2;
    public int MaxActiveConstructionProjects { get; set; } = 1;
    public int ConstructionMonths { get; set; } = 6;
    public decimal ConstructionVacancyTrigger { get; set; } = 0.005m;
    public decimal HomelessConstructionTrigger { get; set; } = 0.07m;
    public decimal SeedVacancyRate { get; set; } = 0.01m;
    public Money DefaultPropertyUpkeep { get; set; } = Money.FromUnits(50m);
    public int DefaultDistrictCapacity { get; set; } = 2_000;

    public static HousingOptions Default { get; } = new();

    public HousingOptions Clone() => new()
    {
        AffordabilityRatio = AffordabilityRatio,
        RentBurdenTrigger = RentBurdenTrigger,
        TargetVacancyRate = TargetVacancyRate,
        VacancyResponse = VacancyResponse,
        MaxIndexStep = MaxIndexStep,
        MinRentIndex = MinRentIndex,
        MaxRentIndex = MaxRentIndex,
        AnnualRentAdjustmentCap = AnnualRentAdjustmentCap,
        MaxEvictionsPerCycle = MaxEvictionsPerCycle,
        NoticeMonths = NoticeMonths,
        MoveCooldownMonths = MoveCooldownMonths,
        OverburdenMonthsBeforeMove = OverburdenMonthsBeforeMove,
        MaxActiveConstructionProjects = MaxActiveConstructionProjects,
        ConstructionMonths = ConstructionMonths,
        ConstructionVacancyTrigger = ConstructionVacancyTrigger,
        HomelessConstructionTrigger = HomelessConstructionTrigger,
        SeedVacancyRate = SeedVacancyRate,
        DefaultPropertyUpkeep = DefaultPropertyUpkeep,
        DefaultDistrictCapacity = DefaultDistrictCapacity
    };
}
