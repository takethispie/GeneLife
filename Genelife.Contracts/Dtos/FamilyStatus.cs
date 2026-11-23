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
    public int BirthsThisYear { get; set; }
    public decimal BirthRate { get; set; }
    public int NaturalChange { get; set; }
    public int Orphans { get; set; }
    public int StateCareChildren { get; set; }
    public decimal MinorTrustTotal { get; set; }
    public decimal InheritancePaidThisYear { get; set; }
    public decimal TrustReleasedThisYear { get; set; }
    public decimal ReplacementRate { get; set; }
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

public sealed class PregnancyStatusDto
{
    public ulong NetworkId { get; set; }
    public ulong CarryingParentNetworkId { get; set; }
    public ulong OtherParentNetworkId { get; set; }
    public string CarryingParentName { get; set; } = string.Empty;
    public long ConceivedTick { get; set; }
    public long DueTick { get; set; }
    public int MonthsRemaining { get; set; }
    public int RiskBasisPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int Complications { get; set; }
    public decimal CarePaid { get; set; }
    public decimal CareArrears { get; set; }
}

public sealed class ChildStatusDto
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public ulong? ParentANetworkId { get; set; }
    public ulong? ParentBNetworkId { get; set; }
    public ulong? GuardianNetworkId { get; set; }
    public ulong HouseholdNetworkId { get; set; }
    public string Household { get; set; } = string.Empty;
    public bool Enrolled { get; set; }
    public bool Unenrolled { get; set; }
    public string School { get; set; } = string.Empty;
    public bool Overcrowded { get; set; }
    public decimal ChildCostsThisYear { get; set; }
}

public sealed class GuardianshipStatusDto
{
    public ulong GuardianNetworkId { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public ulong ChildNetworkId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public long SinceTick { get; set; }
}
