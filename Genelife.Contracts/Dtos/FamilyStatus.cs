namespace Genelife.Api.DTOs;

public sealed class FamilyStatus
{
    public int Population { get; set; }
    public int Adults { get; set; }
    public int Minors { get; set; }
    public int PairedSims { get; set; }
    public int SingleAdults { get; set; }
    public int Partnerships { get; set; }
    public int ParentEdges { get; set; }
    public int GuardianEdges { get; set; }
    public int ExPartnerships { get; set; }
    public decimal SeedPartnershipShare { get; set; }
    public bool SameSexPairingAllowed { get; set; }
    public bool CohabitOnPairing { get; set; }
    public List<LifecycleHealthCheckStatus> Health { get; set; } = new();
}

public sealed class FamilyRelationshipStatus
{
    public ulong NetworkId { get; set; }
    public ulong FromSimId { get; set; }
    public string FromName { get; set; } = string.Empty;
    public ulong ToSimId { get; set; }
    public string ToName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public long SinceTick { get; set; }
}

public sealed class FamilyRelationshipPage
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<FamilyRelationshipStatus> Relationships { get; set; } = new();
}

public sealed class AttractionComponentStatus
{
    public string Name { get; set; } = string.Empty;
    public int Points { get; set; }
    public int Max { get; set; }
}

public sealed class AttractionExplanationStatus
{
    public ulong SimId { get; set; }
    public ulong CandidateId { get; set; }
    public bool Eligible { get; set; }
    public string? ReasonCode { get; set; }
    public string? Reason { get; set; }
    public int Score { get; set; }
    public List<AttractionComponentStatus> Components { get; set; } = new();
}
