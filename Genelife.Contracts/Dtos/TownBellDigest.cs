namespace Genelife.Api.DTOs;

/// <summary>
/// One aggregated row of the Town bell digest (GDD ch.08 section 8.5(6)): every
/// occurrence of a single <c>(eventKind, subjectEntityId)</c> pair inside the
/// rolling window, collapsed to a count and the first/last tick it was seen.
/// </summary>
public sealed record DigestEntryDto(
    string EventKind,
    string SubjectEntityType,
    ulong SubjectEntityId,
    int Count,
    long FirstTick,
    long LastTick);

/// <summary>
/// The Town bell digest: one server-computed grouping of the events touching the
/// entities a player is connected to, within a rolling window. The grouping key
/// is computed by the server because a grouping rule is a rule (chapter 07
/// principle 5). Pulled once per session; never pushed as toasts, pop-ups or
/// modal interrupts.
/// </summary>
public sealed record TownBellDigestDto(
    string PlayerId,
    ulong GenosNetworkId,
    long AsOfTick,
    long WindowStartTick,
    long WindowTicks,
    string WindowLabel,
    int EntryCount,
    IReadOnlyList<DigestEntryDto> Entries);

/// <summary>
/// Operator-plane view of the digest projection: what the buffer currently holds
/// across every player, before per-player scoping. Read-only diagnostics, not
/// play-surface data (GDD ch.08 section 8.6).
/// </summary>
public sealed record DigestStatusDto(
    long AsOfTick,
    int BufferedEvents,
    int DistinctEntities,
    long OldestTick,
    long NewestTick,
    IReadOnlyList<DigestEntryDto> Entries);
