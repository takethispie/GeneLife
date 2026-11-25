using Genelife.Values;

namespace Genelife.Consumption;

public sealed class UtilityOptions
{
    public int InitialWaterProduction { get; set; } = 12_000;
    public int InitialWaterCapacity { get; set; } = 12_000;
    public int InitialElectricityProduction { get; set; } = 18_000;
    public int InitialElectricityCapacity { get; set; } = 18_000;

    public int WaterUsagePerOccupant { get; set; } = 2;
    public int ElectricityUsagePerOccupant { get; set; } = 3;
    public int ResidentialWaterDemandPerUnit { get; set; } = 6;
    public int ResidentialElectricityDemandPerUnit { get; set; } = 10;
    public int OfficeWaterDemandPerSeat { get; set; } = 2;
    public int OfficeElectricityDemandPerSeat { get; set; } = 3;
    public int SchoolDemand { get; set; } = 40;
    public int StoreDemand { get; set; } = 25;

    public int StrainedBasisPoints { get; set; } = 8_000;
    public int OverloadedBasisPoints { get; set; } = 10_000;
    public int OfflineBasisPoints { get; set; } = 12_500;
    public int ReducedServiceBasisPoints { get; set; } = 5_000;
    public int SubsidyBasisPoints { get; set; } = 10_000;

    public Money WaterBase { get; set; } = Money.FromUnits(5m);
    public Money WaterPerUnit { get; set; } = Money.FromUnits(1m);
    public Money ElectricityBase { get; set; } = Money.FromUnits(6m);
    public Money ElectricityPerUnit { get; set; } = Money.FromUnits(1.5m);

    public int ReducedArrearsMonths { get; set; } = 12;
    public int CutOffArrearsMonths { get; set; } = 24;

    public static UtilityOptions Default { get; } = new();
}

public readonly record struct UtilityTariff(
    Money WaterBase,
    Money WaterPerUnit,
    Money ElectricityBase,
    Money ElectricityPerUnit,
    int ReducedServiceBasisPoints)
{
    public static UtilityTariff FromOptions(UtilityOptions options) => new(
        options.WaterBase,
        options.WaterPerUnit,
        options.ElectricityBase,
        options.ElectricityPerUnit,
        options.ReducedServiceBasisPoints);

    public Money WaterBill(int usage) =>
        (WaterBase + WaterPerUnit * Math.Max(0, usage)).Max(Money.Zero);

    public Money ElectricityBill(int usage) =>
        (ElectricityBase + ElectricityPerUnit * Math.Max(0, usage)).Max(Money.Zero);
}
