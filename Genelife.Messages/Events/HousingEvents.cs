namespace Genelife.Messages.Events;

public record PropertyCreated(
    ulong PropertyId,
    ulong OwnerId,
    string Name,
    string District,
    int Units,
    string Tier,
    long Tick);

public record PropertyUpdated(ulong PropertyId, string Name, float Condition, long UpkeepCents);

public record HousingUnitUpdated(
    ulong UnitId,
    string Tier,
    int Capacity,
    long BaseRentCents,
    float Condition,
    bool IsVacant);

public record TenancyStarted(
    ulong HouseholdId,
    ulong UnitId,
    ulong LandlordId,
    string Tenure,
    long MonthlyRentCents,
    long Tick);

public record RentPaid(
    ulong HouseholdId,
    ulong UnitId,
    ulong LandlordId,
    long AmountCents,
    long Tick);

public record RentMissed(
    ulong HouseholdId,
    ulong UnitId,
    long DueCents,
    long PaidCents,
    int ArrearsCycles,
    long Tick);

public record RentArrearsCleared(ulong HouseholdId, long Tick);

public record LateFeeApplied(ulong HouseholdId, long FeeCents, int ArrearsCycles, long Tick);

public record UtilityBillPaid(
    ulong HouseholdId,
    ulong UnitId,
    long AmountCents,
    long Tick);

public record UtilityArrearsRecorded(
    ulong HouseholdId,
    long DueCents,
    long PaidCents,
    long ArrearsCents,
    long Tick);

public record MaintenancePaid(
    ulong PropertyId,
    ulong PayerId,
    long AmountCents,
    bool IsMunicipality,
    long Tick);

public record MaintenanceSkipped(
    ulong PropertyId,
    ulong PayerId,
    long DueCents,
    long PaidCents,
    long Tick);

public record UnitCondemned(ulong PropertyId, ulong UnitId, float Condition, long Tick);

public record TenantEvicted(
    ulong HouseholdId,
    ulong UnitId,
    long ArrearsCents,
    int ArrearsCycles,
    long Tick);

public record TenantNoticeIssued(
    ulong HouseholdId,
    ulong UnitId,
    int Stage,
    long CureDeadlineTick,
    long Tick);

public record HouseholdRelocated(
    ulong HouseholdId,
    ulong FromUnitId,
    ulong ToUnitId,
    long Tick);

public record HouseholdHomeless(ulong HouseholdId, long Tick);

public record HouseholdMoved(
    ulong HouseholdId,
    ulong FromUnitId,
    ulong ToUnitId,
    long DepositCents,
    long FeeCents,
    long Tick);

public record HomePurchased(
    ulong HouseholdId,
    ulong UnitId,
    ulong SellerId,
    long PriceCents,
    long Tick);

public record HomeSold(
    ulong HouseholdId,
    ulong UnitId,
    ulong BuyerId,
    long PriceCents,
    long Tick);

public record ConstructionStarted(
    ulong ProjectId,
    ulong DeveloperId,
    string District,
    string Tier,
    int Units,
    long CostCents,
    int Months,
    long Tick);

public record ConstructionCompleted(
    ulong ProjectId,
    ulong PropertyId,
    int Units,
    long Tick);

public record ConstructionCancelled(ulong ProjectId, string Reason, long Tick);

public record PropertyBuilt(ulong PropertyId, ulong OwnerId, string District, int Units, long Tick);

public record HousingMarketUpdated(
    decimal Index,
    decimal VacancyRate,
    int VacantUnits,
    int TotalUnits,
    long Tick);

public record HouseholdFormed(ulong HouseholdId, ulong HeadId, long Tick);

public record HouseholdMemberJoined(ulong HouseholdId, ulong GenosId, string Role, long Tick);

public record HouseholdMemberLeft(ulong HouseholdId, ulong GenosId, long Tick);

public record HouseholdDissolved(ulong HouseholdId, long Tick);

public record HouseholdHeadChanged(ulong HouseholdId, ulong HeadId, long Tick);

public record PopulationSpawned(
    int Adults,
    int Children,
    int Households,
    int Housed,
    int Sheltered,
    int Homeless,
    long Tick);

public record HousingMigrated(int Households, int Units, long Tick);

public record HousingSnapshotTaken(
    long Tick,
    int Year,
    int Properties,
    int Units,
    int OccupiedUnits,
    int VacantUnits,
    int ShelterBeds,
    int ShelteredHouseholds,
    int HomelessHouseholds,
    int OvercrowdedUnits,
    int UnitsBelowCondition,
    decimal VacancyRate,
    decimal RentIndex,
    long RentRollCents,
    long RentCollectedCents,
    long RentArrearsCents,
    long UtilityArrearsCents,
    long UpkeepPaidCents,
    int EvictionsThisYear,
    int ConstructionUnits,
    decimal MedianRentBurden,
    decimal ShelterOccupancy);
