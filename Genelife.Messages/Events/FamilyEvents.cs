namespace Genelife.Messages.Events;

public sealed record RelationshipFormed(
    ulong RelationshipId,
    ulong FromSimId,
    ulong ToSimId,
    string Kind,
    string Source,
    long Tick);

public sealed record RelationshipEnded(ulong RelationshipId, string Kind, long Tick);

public sealed record PartnershipFormed(ulong SimAId, ulong SimBId, int Score, long Tick);

public sealed record PairingRejected(ulong SimId, ulong CandidateId, string Reason, long Tick);

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
