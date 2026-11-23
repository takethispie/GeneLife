namespace Genelife.Messages.Events;

public sealed record UtilityNetworkStateChanged(
    ulong NetworkId,
    string Service,
    int Production,
    int Capacity,
    int Load,
    int Reserve,
    string FromState,
    string ToState,
    long Tick);

public sealed record UtilityServiceCutOff(
    ulong HouseholdNetworkId,
    string Service,
    string Level,
    string Cause,
    long Tick);

public sealed record GroceryPurchase(
    ulong HouseholdNetworkId,
    ulong StoreNetworkId,
    string ItemId,
    int Units,
    long AmountCents,
    long Tick);

public sealed record GrocerySpoiled(
    ulong OwnerNetworkId,
    string ItemId,
    int Units,
    string Reason,
    long Tick);

public sealed record GroceryRestocked(
    ulong StoreNetworkId,
    int Units,
    long CostCents,
    long Tick);

public sealed record UtilityOutageApplied(
    ulong HouseholdNetworkId,
    string WaterLevel,
    string ElectricityLevel,
    float HygieneDelta,
    float EnergyDelta,
    int SpoiledFood,
    long Tick);

public sealed record ConsumptionAdminActionApplied(
    Guid CommandId,
    string Action,
    string IssuedBy,
    string Target,
    string Reason,
    long Tick);

public sealed record ConsumptionMetricsRecorded(
    long Tick,
    int Year,
    int WaterProduction,
    int WaterCapacity,
    int WaterLoad,
    int ElectricityProduction,
    int ElectricityCapacity,
    int ElectricityLoad,
    int Households,
    int CutOffHouseholds,
    int ReducedHouseholds,
    int Stores,
    int StockUnits,
    int SpoiledUnitsThisYear,
    int MalnourishedHouseholds,
    long UtilityRevenueCents,
    long GroceryRevenueCents);

public sealed record NutritionHealthChanged(
    ulong SimNetworkId,
    int Balance,
    int ConsecutivePoorDays,
    float HealthDelta,
    long Tick);
