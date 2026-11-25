namespace Genelife.Messages.Events;

public sealed record RelationshipFormed(
    ulong RelationshipId,
    ulong FromGenosId,
    ulong ToGenosId,
    string Kind,
    string Source,
    long Tick);

public sealed record RelationshipEnded(
    ulong RelationshipId,
    ulong FromGenosId,
    ulong ToGenosId,
    string Kind,
    long Tick);

public sealed record PartnershipFormed(ulong GenosAId, ulong GenosBId, int Score, long Tick);

public sealed record PartnershipEnded(ulong GenosAId, ulong GenosBId, string Reason, long Tick);

public sealed record PairingRejected(ulong GenosId, ulong CandidateId, string Reason, long Tick);

public sealed record HouseholdMerged(ulong HouseholdId, ulong IntoHouseholdId, long Tick);

public sealed record HouseholdSplit(ulong HouseholdId, ulong FromHouseholdId, long Tick);

public sealed record PregnancyStarted(
    ulong PregnancyNetworkId,
    ulong CarryingParentNetworkId,
    ulong OtherParentNetworkId,
    long ConceivedTick,
    long DueTick,
    long Tick);

public sealed record PregnancyProgressed(
    ulong PregnancyNetworkId,
    int RiskBasisPoints,
    int MonthsRemaining,
    long Tick);

public sealed record PregnancyComplication(
    ulong PregnancyNetworkId,
    string Kind,
    int RiskBasisPoints,
    long Tick);

public sealed record PregnancyLost(
    ulong PregnancyNetworkId,
    string Cause,
    long Tick);

public sealed record PregnancyCancelled(
    ulong PregnancyNetworkId,
    string Reason,
    long Tick);

public sealed record ChildBorn(
    ulong ChildNetworkId,
    ulong ParentANetworkId,
    ulong ParentBNetworkId,
    ulong HouseholdNetworkId,
    long Tick);

public sealed record BirthRecorded(
    ulong ChildNetworkId,
    ulong PregnancyNetworkId,
    string Name,
    long Tick);

public sealed record GuardianshipAssigned(
    ulong ChildNetworkId,
    ulong GuardianNetworkId,
    string Source,
    long Tick);

public sealed record StateCareOpened(
    ulong ChildNetworkId,
    long MonthlyCostCents,
    long Tick);

public sealed record StateCareClosed(
    ulong ChildNetworkId,
    ulong GuardianNetworkId,
    string Reason,
    long Tick);

public sealed record FamilyAdminActionApplied(
    Guid CommandId,
    string Action,
    string IssuedBy,
    string Reason,
    ulong SubjectA,
    ulong SubjectB,
    ulong SubjectC,
    long Tick);

public sealed record ChildCostPaid(
    ulong ChildNetworkId,
    ulong HouseholdNetworkId,
    string Category,
    long AmountCents,
    long Tick);

public sealed record ChildBenefitPaid(
    ulong HouseholdNetworkId,
    int Children,
    long AmountCents,
    long Tick);

public sealed record ConceptionOccurred(
    ulong CarryingParentNetworkId,
    ulong OtherParentNetworkId,
    ulong PregnancyNetworkId,
    long Tick);

public sealed record TrustOpened(
    ulong TrustNetworkId,
    ulong BeneficiaryNetworkId,
    ulong GuardianNetworkId,
    long AmountCents,
    ulong SourceGenosNetworkId,
    long Tick);

public sealed record TrustReleased(
    ulong TrustNetworkId,
    ulong BeneficiaryNetworkId,
    long AmountCents,
    string Reason,
    long Tick);
