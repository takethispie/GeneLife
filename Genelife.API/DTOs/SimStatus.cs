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
    public decimal WalletBalance { get; set; }
    public decimal SavingsBalance { get; set; }
    public decimal UnmetDemand { get; set; }
    public ulong? HouseholdNetworkId { get; set; }
    public ulong? HousingUnitNetworkId { get; set; }
    public string Tenure { get; set; } = string.Empty;
    public string HousingTier { get; set; } = string.Empty;
    public bool Homeless { get; set; }
}
