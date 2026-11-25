namespace Genelife.Social;

public enum ModerationActionKind : byte
{
    Warn,
    Mute,
    Block,
    RemoveMessage,
    CloseVenue,
    SuspendChat,
    Unmute,
    Unblock,
    ReopenVenue,
    ResumeChat
}

/// <summary>
/// Audited moderation decision. Evidence is copied into the action so report or
/// message deletion cannot erase the reason a moderator acted.
/// </summary>
public sealed record ModerationAction
{
    public Guid Id { get; init; }
    public Guid? ReportId { get; init; }
    public Guid? ReversesActionId { get; init; }
    public string ModeratorId { get; init; } = string.Empty;
    public string TargetPlayerId { get; init; } = string.Empty;
    public string? CounterpartyPlayerId { get; init; }
    public ModerationActionKind Kind { get; init; }
    public long DurationTicks { get; init; }
    public long CreatedTick { get; init; }
    public long? EndsTick { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Evidence { get; init; }
    public Guid? MessageId { get; init; }
    public ulong? VenueNetworkId { get; init; }
}

public sealed record ModerationRequest(
    ModerationActionKind Kind,
    string TargetPlayerId,
    string Reason,
    Guid? ReportId = null,
    long DurationTicks = 0,
    Guid? MessageId = null,
    ulong? VenueNetworkId = null);

public sealed record ModerationResult(
    bool Applied,
    string? Code,
    string? Message,
    ModerationAction? Action)
{
    public static ModerationResult Reject(string code, string message) =>
        new(false, code, message, null);
}

public sealed record ModerationQueueItem(
    SocialReport Report,
    int TargetReportCount,
    int DistinctReporters);

public sealed record RepeatOffender(
    string PlayerId,
    int ReportCount,
    int DistinctReporters,
    long OldestReportTick);

public sealed record ModerationQueue(
    IReadOnlyList<ModerationQueueItem> Items,
    IReadOnlyList<RepeatOffender> RepeatOffenders);

public sealed class ModerationOptions
{
    public long DefaultMuteTicks { get; set; } = 300;
    public long DefaultSuspendChatTicks { get; set; } = 300;
    public long ReportRetentionTicks { get; set; } = 1200;
    public long ActionRetentionTicks { get; set; } = 2400;
    public long ReportWindowTicks { get; set; } = 1200;
    public int RepeatOffenderThreshold { get; set; } = 3;
    public int QueueTake { get; set; } = 100;
    public int RetentionSweepMinutes { get; set; } = 60;

    public void Validate()
    {
        if (DefaultMuteTicks < 1 || DefaultSuspendChatTicks < 1)
            throw new InvalidOperationException("Moderation default durations must be positive.");
        if (ReportRetentionTicks < 1 || ActionRetentionTicks < ReportRetentionTicks)
            throw new InvalidOperationException("Moderation action retention must cover report retention.");
        if (ReportWindowTicks < 1 || RepeatOffenderThreshold < 2)
            throw new InvalidOperationException("Moderation report window and threshold are invalid.");
        if (QueueTake < 1 || RetentionSweepMinutes < 1)
            throw new InvalidOperationException("Moderation queue and retention settings are invalid.");
    }
}

public sealed record SocialRestriction(
    Guid ActionId,
    string PlayerId,
    long UntilTick,
    bool ChatBlocked,
    bool ActivityBlocked,
    bool InvitationsBlocked,
    string Reason,
    long CreatedTick);
