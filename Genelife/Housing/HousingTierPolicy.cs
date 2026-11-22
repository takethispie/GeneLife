using Genelife.Enums;
using Genelife.Values;

namespace Genelife.Housing;

/// <summary>
/// Base values per housing tier. Keep this table as the single place that
/// describes the rent ladder, capacity and utility baseline.
/// </summary>
public static class HousingTierPolicy
{
    public static Money BaseRent(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => Money.FromUnits(10m),
        HousingTier.Basic => Money.FromUnits(120m),
        HousingTier.Comfortable => Money.FromUnits(200m),
        HousingTier.Luxury => Money.FromUnits(360m),
        _ => Money.FromUnits(120m)
    };

    public static int Capacity(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => 1,
        HousingTier.Basic => 3,
        HousingTier.Comfortable => 4,
        HousingTier.Luxury => 6,
        _ => 3
    };

    public static float Quality(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => 0.05f,
        HousingTier.Basic => 0.6f,
        HousingTier.Comfortable => 0.85f,
        HousingTier.Luxury => 1f,
        _ => 0.6f
    };

    public static Money BaseUtility(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => Money.FromUnits(5m),
        HousingTier.Basic => Money.FromUnits(35m),
        HousingTier.Comfortable => Money.FromUnits(65m),
        HousingTier.Luxury => Money.FromUnits(110m),
        _ => Money.FromUnits(35m)
    };

    public static Money ConstructionCostPerUnit(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => Money.FromUnits(8_000m),
        HousingTier.Basic => Money.FromUnits(18_000m),
        HousingTier.Comfortable => Money.FromUnits(30_000m),
        HousingTier.Luxury => Money.FromUnits(55_000m),
        _ => Money.FromUnits(18_000m)
    };

    public static Money PurchasePrice(HousingTier tier) =>
        BaseRent(tier) * 240;

    public static HousingTier Next(HousingTier tier) => tier switch
    {
        HousingTier.Shelter => HousingTier.Basic,
        HousingTier.Basic => HousingTier.Comfortable,
        HousingTier.Comfortable => HousingTier.Luxury,
        _ => HousingTier.Luxury
    };

    public static HousingTier Previous(HousingTier tier) => tier switch
    {
        HousingTier.Luxury => HousingTier.Comfortable,
        HousingTier.Comfortable => HousingTier.Basic,
        _ => HousingTier.Shelter
    };
}
