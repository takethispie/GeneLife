namespace Genelife.Api.Models;

/// <summary>
/// Durable family summary per sim. The live engine is authoritative for the
/// current tick; this document is the rebuildable history.
/// </summary>
public sealed class FamilyDocument
{
    public Guid Id { get; set; }
    public long SimNetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public long PartnerNetworkId { get; set; }
    public long? GuardianNetworkId { get; set; }
    public bool InStateCare { get; set; }
    public bool IsDead { get; set; }
    public HashSet<long> ParentIds { get; set; } = new();
    public HashSet<long> ChildIds { get; set; } = new();
    public HashSet<long> WardIds { get; set; } = new();
    public HashSet<long> TrustIds { get; set; } = new();
    public long InheritanceCents { get; set; }
    public long TrustReleasedCents { get; set; }
    public long UpdatedTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RelationshipDocument
{
    public Guid Id { get; set; }
    public long RelationshipNetworkId { get; set; }
    public long FromSimNetworkId { get; set; }
    public long ToSimNetworkId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public long SinceTick { get; set; }
    public long? EndedTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PregnancyDocument
{
    public Guid Id { get; set; }
    public long PregnancyNetworkId { get; set; }
    public long CarryingParentNetworkId { get; set; }
    public long OtherParentNetworkId { get; set; }
    public long ConceivedTick { get; set; }
    public long DueTick { get; set; }
    public int RiskBasisPoints { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int ChildrenExpected { get; set; }
    public long CarePaidCents { get; set; }
    public long CareArrearsCents { get; set; }
    public long ResolvedTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChildDocument
{
    public Guid Id { get; set; }
    public long ChildNetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ParentANetworkId { get; set; }
    public long? ParentBNetworkId { get; set; }
    public long? GuardianNetworkId { get; set; }
    public long HouseholdNetworkId { get; set; }
    public bool InStateCare { get; set; }
    public long BornTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HouseholdCompositionDocument
{
    public Guid Id { get; set; }
    public long HouseholdNetworkId { get; set; }
    public int Members { get; set; }
    public int Children { get; set; }
    public int Dependants { get; set; }
    public string Source { get; set; } = "projection";
    public long UpdatedTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EstateDocument
{
    public Guid Id { get; set; }
    public long DeceasedNetworkId { get; set; }
    public long AssetsCents { get; set; }
    public long DebtsCents { get; set; }
    public long DistributedCents { get; set; }
    public long EscheatCents { get; set; }
    public long InheritancePaidCents { get; set; }
    public long TrustsOpenedCents { get; set; }
    public int Heirs { get; set; }
    public long UpdatedTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FamilyAdminActionDocument
{
    public Guid Id { get; set; }
    public Guid CommandId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public long SubjectA { get; set; }
    public long SubjectB { get; set; }
    public long SubjectC { get; set; }
    public long Tick { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
