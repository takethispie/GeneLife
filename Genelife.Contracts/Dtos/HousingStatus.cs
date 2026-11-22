namespace Genelife.Api.DTOs;

public sealed class HousingStatus
{
    public int Properties { get; set; }
    public int Units { get; set; }
    public int OccupiedUnits { get; set; }
    public int VacantUnits { get; set; }
    public int ShelterBeds { get; set; }
    public int ShelteredHouseholds { get; set; }
    public int HomelessHouseholds { get; set; }
    public int OvercrowdedUnits { get; set; }
    public int UnitsBelowCondition { get; set; }
    public int ActiveProjects { get; set; }
    public int ConstructionUnits { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal RentIndex { get; set; }
    public decimal RentRoll { get; set; }
    public decimal RentCollectedThisMonth { get; set; }
    public decimal UtilityCollectedThisMonth { get; set; }
    public decimal UpkeepPaidThisMonth { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
    public decimal MedianRentBurden { get; set; }
    public decimal ShelterOccupancy { get; set; }
    public int EvictionsThisYear { get; set; }
    public int MovesThisYear { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public List<HousingHealthCheckStatus> Health { get; set; } = new();
}

public sealed class HousingHealthCheckStatus
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class HousingPropertyStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public ulong? OwnerNetworkId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty;
    public int UnitCount { get; set; }
    public int OccupiedUnits { get; set; }
    public int VacantUnits { get; set; }
    public float Condition { get; set; }
    public float Neighbourhood { get; set; }
    public decimal MonthlyUpkeep { get; set; }
    public bool IsShelter { get; set; }
    public List<HousingUnitStatus> Units { get; set; } = new();
}

public sealed class HousingUnitStatus
{
    public ulong NetworkId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Occupants { get; set; }
    public decimal BaseRent { get; set; }
    public decimal MarketRent { get; set; }
    public decimal PurchasePrice { get; set; }
    public float Condition { get; set; }
    public bool IsVacant { get; set; }
    public bool IsOvercrowded { get; set; }
    public bool IsCondemned { get; set; }
    public ulong? HouseholdNetworkId { get; set; }
    public string Household { get; set; } = string.Empty;
}

public sealed class HouseholdStatus
{
    public ulong NetworkId { get; set; }
    public ulong HeadNetworkId { get; set; }
    public string Head { get; set; } = string.Empty;
    public ulong? UnitNetworkId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string Tenure { get; set; } = string.Empty;
    public int Members { get; set; }
    public int Earners { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
    public decimal Debt { get; set; }
    public int ArrearsCycles { get; set; }
    public int NoticeStage { get; set; }
    public long CureDeadlineTick { get; set; }
    public bool Overcrowded { get; set; }
    public bool NoUtilities { get; set; }
    public bool Homeless { get; set; }
    public List<string> MembersList { get; set; } = new();
}

public sealed class HousingRentRollStatus
{
    public ulong? OwnerNetworkId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string OwnerType { get; set; } = string.Empty;
    public int Units { get; set; }
    public int OccupiedUnits { get; set; }
    public decimal RentRoll { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
}

public sealed class HousingSnapshotStatus
{
    public long Tick { get; set; }
    public int Year { get; set; }
    public int Properties { get; set; }
    public int Units { get; set; }
    public int OccupiedUnits { get; set; }
    public int VacantUnits { get; set; }
    public int ShelterBeds { get; set; }
    public int ShelteredHouseholds { get; set; }
    public int HomelessHouseholds { get; set; }
    public int OvercrowdedUnits { get; set; }
    public int UnitsBelowCondition { get; set; }
    public decimal VacancyRate { get; set; }
    public decimal RentIndex { get; set; }
    public decimal RentRoll { get; set; }
    public decimal RentCollected { get; set; }
    public decimal RentArrears { get; set; }
    public decimal UtilityArrears { get; set; }
    public decimal UpkeepPaid { get; set; }
    public int EvictionsThisYear { get; set; }
    public int ConstructionUnits { get; set; }
    public decimal MedianRentBurden { get; set; }
    public decimal ShelterOccupancy { get; set; }
}

public sealed class HousingConstructionStatus
{
    public ulong NetworkId { get; set; }
    public ulong? DeveloperNetworkId { get; set; }
    public string Developer { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int Units { get; set; }
    public int MonthsRemaining { get; set; }
    public decimal Cost { get; set; }
    public decimal PaidToDate { get; set; }
}

public sealed class SpawnPopulationStatus
{
    public int Adults { get; set; }
    public int Children { get; set; }
    public int Households { get; set; }
    public int Housed { get; set; }
    public int Sheltered { get; set; }
    public int Homeless { get; set; }
}
