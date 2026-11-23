namespace Genelife.Api.DTOs;

public class SimStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsPlayerControlled { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public bool HasMoveTarget { get; set; }
    public int MoveTargetX { get; set; }
    public int MoveTargetY { get; set; }
    public decimal WalletBalance { get; set; }
    public decimal SavingsBalance { get; set; }
    public decimal UnmetDemand { get; set; }
    public ulong? HouseholdNetworkId { get; set; }
    public ulong? HousingUnitNetworkId { get; set; }
    public string Tenure { get; set; } = string.Empty;
    public string HousingTier { get; set; } = string.Empty;
    public bool Homeless { get; set; }
    public string Stage { get; set; } = string.Empty;
    public decimal Health { get; set; }
    public string TopSkill { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public bool IsDead { get; set; }
    public AppearanceDto? Appearance { get; set; }
}
