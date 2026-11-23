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
