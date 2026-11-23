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
    public decimal PartnershipRate { get; set; }
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
    public TraitDistributionStatus Traits { get; set; } = new();
    public List<HouseholdCompositionStatus> Households { get; set; } = new();
    public ProjectionStalenessStatus Projection { get; set; } = new();
    public FamilyPerformanceStatus Performance { get; set; } = new();
    public List<FamilyHealthCheckStatus> Health { get; set; } = new();
}

public sealed class FamilyPerformanceStatus
{
    public double LastTickMilliseconds { get; set; }
    public double P95TickMilliseconds { get; set; }
    public double P99TickMilliseconds { get; set; }
    public double AllocatedBytesPerTick { get; set; }
    public int CandidateSims { get; set; }
    public int CandidateAverage { get; set; }
    public int CandidateP95 { get; set; }
    public int CandidateMax { get; set; }
    public int CandidateCap { get; set; }
    public string WorstSystem { get; set; } = string.Empty;
    public double WorstSystemAverageMilliseconds { get; set; }
}

public sealed class TraitDistributionStatus
{
    public int Population { get; set; }
    public int WithTraits { get; set; }
    public int MissingTraits { get; set; }
    public int Migrated { get; set; }
    public int MutationEvents { get; set; }
    public decimal FemaleShare { get; set; }
    public List<TraitCategoryStatus> Categories { get; set; } = new();
}

public sealed class TraitCategoryStatus
{
    public string Name { get; set; } = string.Empty;
    public List<TraitValueStatus> Values { get; set; } = new();
}

public sealed class TraitValueStatus
{
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Share { get; set; }
}

public sealed class FamilyHealthCheckStatus
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public ulong? EntityNetworkId { get; set; }
    public string? EntityKind { get; set; }
    public string? Link { get; set; }
}

public sealed class ProjectionStalenessStatus
{
    public string Source { get; set; } = "live";
    public long LiveTick { get; set; }
    public long? ProjectedTick { get; set; }
    public bool IsStale { get; set; }
    public string Detail { get; set; } = "Family read model reads live simulation state.";
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

public sealed class FamilySimStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public ulong HouseholdNetworkId { get; set; }
    public string Household { get; set; } = string.Empty;
    public ulong? PartnerNetworkId { get; set; }
    public string? PartnerName { get; set; }
    public ulong? GuardianNetworkId { get; set; }
    public bool IsPaired { get; set; }
    public bool HasChildren { get; set; }
    public bool IsOrphan { get; set; }
    public bool InStateCare { get; set; }
    public int ParentCount { get; set; }
    public int ChildCount { get; set; }
    public List<TraitValueStatus> Traits { get; set; } = new();
}

public sealed class FamilySimPage
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<FamilySimStatus> Sims { get; set; } = new();
}

public sealed class FamilySimDetailStatus
{
    public FamilySimStatus Sim { get; set; } = new();
    public List<FamilyRelationshipStatus> Relationships { get; set; } = new();
    public List<PregnancyStatusDto> Pregnancies { get; set; } = new();
    public List<ChildStatusDto> Children { get; set; } = new();
    public List<GuardianshipStatusDto> Guardianships { get; set; } = new();
    public HouseholdCompositionStatus? Household { get; set; }
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
    public string OtherParentName { get; set; } = string.Empty;
    public long ConceivedTick { get; set; }
    public long DueTick { get; set; }
    public int MonthsRemaining { get; set; }
    public int RiskBasisPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int ChildrenExpected { get; set; }
    public int Complications { get; set; }
    public int CareMonths { get; set; }
    public int MissedCareMonths { get; set; }
    public decimal CarePaid { get; set; }
    public decimal CareArrears { get; set; }
    public long ResolvedTick { get; set; }
}

public sealed class PregnancyPageStatus
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool ActiveOnly { get; set; }
    public List<PregnancyStatusDto> Pregnancies { get; set; } = new();
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
    public bool InStateCare { get; set; }
    public decimal ChildCostsThisYear { get; set; }
}

public sealed class ChildPageStatus
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public string? AgeBand { get; set; }
    public List<ChildStatusDto> Children { get; set; } = new();
}

public sealed class GuardianshipStatusDto
{
    public ulong GuardianNetworkId { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public ulong ChildNetworkId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public long SinceTick { get; set; }
    public bool StateCare { get; set; }
}

public sealed class HouseholdCompositionStatus
{
    public ulong NetworkId { get; set; }
    public ulong HeadNetworkId { get; set; }
    public string Head { get; set; } = string.Empty;
    public int Members { get; set; }
    public int Children { get; set; }
    public int Dependants { get; set; }
    public int Earners { get; set; }
    public int Capacity { get; set; }
    public int OccupantCount { get; set; }
    public bool Overcrowded { get; set; }
    public ulong? UnitNetworkId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Tenure { get; set; } = string.Empty;
}
