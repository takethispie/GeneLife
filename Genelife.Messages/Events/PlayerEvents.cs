namespace Genelife.Messages.Events;

public sealed record PlayerProfileCreated(ulong GenosNetworkId, string PlayerId, long Tick);

public sealed record PlayerHired(
    string PlayerId,
    ulong GenosNetworkId,
    ulong CompanyNetworkId,
    ulong OfficeNetworkId,
    string Position,
    long SalaryCents,
    long Tick);

public sealed record PlayerQuitJob(
    string PlayerId,
    ulong GenosNetworkId,
    ulong CompanyNetworkId,
    string Reason,
    long Tick);

public sealed record PlayerTerminated(
    string PlayerId,
    ulong GenosNetworkId,
    ulong CompanyNetworkId,
    string Reason,
    long Tick);

public sealed record PlayerLeaseStarted(
    string PlayerId,
    ulong GenosNetworkId,
    ulong HouseholdNetworkId,
    ulong UnitNetworkId,
    long RentCents,
    long Tick);

public sealed record PlayerMoved(
    string PlayerId,
    ulong GenosNetworkId,
    ulong HouseholdNetworkId,
    ulong FromUnitNetworkId,
    ulong ToUnitNetworkId,
    long Tick);

public sealed record PlayerArrearsPaid(
    ulong GenosNetworkId,
    ulong HouseholdNetworkId,
    long AmountCents,
    long Tick);

public sealed record PlayerBoughtItem(
    ulong GenosNetworkId,
    ulong StoreNetworkId,
    string CatalogItemId,
    int Quantity,
    long AmountCents,
    long Tick);

public sealed record PlayerConsumedItem(
    ulong GenosNetworkId,
    string CatalogItemId,
    int Quantity,
    long Tick);

public sealed record PlayerInvited(
    ulong FromGenosNetworkId,
    ulong ToGenosNetworkId,
    Guid InvitationId,
    string Kind,
    long ExpiresTick,
    long Tick);

public sealed record PlayerInvitationResponded(
    ulong FromGenosNetworkId,
    ulong ToGenosNetworkId,
    Guid InvitationId,
    string Kind,
    bool Accepted,
    string? Code,
    long Tick);

public sealed record PlayerChildNamed(
    ulong ParentGenosNetworkId,
    ulong ChildGenosNetworkId,
    string Name,
    long Tick);

public sealed record PlayerAwaitingContinuation(
    ulong GenosNetworkId,
    string PlayerId,
    long Tick,
    string? Name = null,
    int Age = 0,
    string? Cause = null,
    bool CanRespawn = false);

public sealed record PlayerContinuationApplied(
    string PlayerId,
    ulong PreviousGenosNetworkId,
    ulong NewGenosNetworkId,
    string Policy,
    string? Code,
    long Tick,
    string? Name = null);

/// <summary>
/// Audit fact for every admitted player action. Written off the tick thread and
/// rebuildable; it is not part of the world snapshot.
/// </summary>
public sealed record PlayerCommandApplied(
    Guid CommandId,
    string PlayerId,
    ulong GenosNetworkId,
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
