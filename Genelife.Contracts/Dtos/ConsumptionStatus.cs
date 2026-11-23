namespace Genelife.Api.DTOs;

public sealed record UtilityNetworkStatus(
    ulong NetworkId,
    string Service,
    int Production,
    int Capacity,
    int Load,
    int Reserve,
    string State);

public sealed record UtilityLinkStatus(
    ulong NetworkId,
    ulong BuildingNetworkId,
    string Service,
    int Demand,
    bool Connected,
    int Priority);

public sealed record ConsumptionHouseholdStatus(
    ulong HouseholdId,
    ulong UnitId,
    int Occupants,
    int WaterUsage,
    int ElectricityUsage,
    string WaterLevel,
    string ElectricityLevel,
    long ArrearsCents,
    long RentArrearsCents,
    bool NoUtilities,
    int ConsecutivePoorMonths);

public sealed record StoreItemStatus(
    int ItemIndex,
    string ItemId,
    int Stock,
    long PriceCents,
    long LastRestockTick);

public sealed record ConsumptionStoreStatus(
    ulong StoreNetworkId,
    string Name,
    IReadOnlyList<StoreItemStatus> Items);

public sealed record NutritionBucketStatus(string Band, int Count);

public sealed record ConsumptionOverviewStatus(
    IReadOnlyList<UtilityNetworkStatus> Networks,
    int Households,
    int CutOffHouseholds,
    int ReducedHouseholds,
    int Stores,
    int StockUnits,
    int SpoiledThisYear,
    IReadOnlyList<NutritionBucketStatus> Nutrition,
    int MalnourishedHouseholds,
    int GroceryCatalogVersion);

public sealed record ConsumptionHouseholdPage(
    IReadOnlyList<ConsumptionHouseholdStatus> Items,
    int Total);

public sealed record ConsumptionStorePage(
    IReadOnlyList<ConsumptionStoreStatus> Items,
    int Total);
