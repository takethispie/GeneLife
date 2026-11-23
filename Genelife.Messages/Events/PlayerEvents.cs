namespace Genelife.Messages.Events;

public sealed record PlayerProfileCreated(ulong SimNetworkId, string PlayerId, long Tick);

public sealed record PlayerHired(
    string PlayerId,
    ulong SimNetworkId,
    ulong CompanyNetworkId,
    ulong OfficeNetworkId,
    string Position,
    long SalaryCents,
    long Tick);

public sealed record PlayerQuitJob(
    string PlayerId,
    ulong SimNetworkId,
    ulong CompanyNetworkId,
    string Reason,
    long Tick);

public sealed record PlayerTerminated(
    string PlayerId,
    ulong SimNetworkId,
    ulong CompanyNetworkId,
    string Reason,
    long Tick);

public sealed record PlayerLeaseStarted(
    string PlayerId,
    ulong SimNetworkId,
    ulong HouseholdNetworkId,
    ulong UnitNetworkId,
    long RentCents,
    long Tick);

public sealed record PlayerMoved(
    string PlayerId,
    ulong SimNetworkId,
    ulong HouseholdNetworkId,
    ulong FromUnitNetworkId,
    ulong ToUnitNetworkId,
    long Tick);

public sealed record PlayerArrearsPaid(
    ulong SimNetworkId,
    ulong HouseholdNetworkId,
    long AmountCents,
    long Tick);

public sealed record PlayerBoughtItem(
    ulong SimNetworkId,
    ulong StoreNetworkId,
    string CatalogItemId,
    int Quantity,
    long AmountCents,
    long Tick);

public sealed record PlayerConsumedItem(
    ulong SimNetworkId,
    string CatalogItemId,
    int Quantity,
    long Tick);

public sealed record PlayerInvited(
    ulong FromSimNetworkId,
    ulong ToSimNetworkId,
    Guid InvitationId,
    string Kind,
    long ExpiresTick,
    long Tick);

public sealed record PlayerInvitationResponded(
    ulong FromSimNetworkId,
    ulong ToSimNetworkId,
    Guid InvitationId,
    string Kind,
    bool Accepted,
    string? Code,
    long Tick);

public sealed record PlayerChildNamed(
    ulong ParentSimNetworkId,
    ulong ChildSimNetworkId,
    string Name,
    long Tick);

public sealed record PlayerAwaitingContinuation(
    ulong SimNetworkId,
    string PlayerId,
    long Tick);

public sealed record PlayerContinuationApplied(
    string PlayerId,
    ulong PreviousSimNetworkId,
    ulong NewSimNetworkId,
    string Policy,
    string? Code,
    long Tick);

/// <summary>
/// Audit fact for every admitted player action. Written off the tick thread and
/// rebuildable; it is not part of the world snapshot.
/// </summary>
public sealed record PlayerCommandApplied(
    Guid CommandId,
    string PlayerId,
    ulong SimNetworkId,
    string Kind,
    string Status,
    string? Code,
    long Tick);

public sealed record PlayerLifeMetricsRecorded(
    int Players,
    int AwaitingContinuation,
    int Employed,
    int Housed,
    long ContinuationsThisYear,
    long DeathsThisYear,
    long CommandsAppliedThisYear);

public sealed record PlayerAdminActionApplied(
    Guid CommandId,
    string Action,
    string IssuedBy,
    string PlayerId,
    string Reason,
    long Tick,
    string? Policy = null);
