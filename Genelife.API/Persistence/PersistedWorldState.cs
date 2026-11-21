namespace Genelife.Api.Persistence;

public sealed class PersistedWorldState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; }
    public long Tick { get; set; }
    public List<PersistedSim> Sims { get; set; } = [];
    public List<PersistedCompany> Companies { get; set; } = [];
}

public sealed class PersistedSim
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public ulong NetworkId { get; set; }
    public float Hunger { get; set; }
    public float Energy { get; set; }
    public float Hygiene { get; set; }
    public float Bladder { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public float TimeRemaining { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsLookingForWork { get; set; }
    public string? PlayerId { get; set; }
    public float WalletBalance { get; set; }
}

public sealed class PersistedCompany
{
    public string Name { get; set; } = string.Empty;
    public ulong NetworkId { get; set; }
}