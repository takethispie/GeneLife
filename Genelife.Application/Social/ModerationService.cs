using Arch.Core;
using Genelife.Api.Services;
using Genelife.Social;

namespace Genelife.Api.Social;

/// <summary>
/// Audited moderation decisions over the social graph, chat store and venue
/// service. Actions never read or edit the world snapshot directly.
/// </summary>
public sealed class ModerationService
{
    private readonly object gate = new();
    private readonly SimulationEngine engine;
    private readonly ChatService chat;
    private readonly SocialGraphService social;
    private readonly ModerationOptions options;
    private readonly ISocialAuditStore? auditStore;
    private readonly List<ModerationAction> actions = new();
    private bool auditLoaded;

    public ModerationService(
        SimulationEngine engine,
        ChatService chat,
        SocialGraphService social,
        ModerationOptions? options = null,
        ISocialAuditStore? auditStore = null)
    {
        this.engine = engine;
        this.chat = chat;
        this.social = social;
        this.options = options ?? new ModerationOptions();
        this.options.Validate();
        this.auditStore = auditStore;
    }

    public async Task<ModerationResult> ApplyAsync(
        ModerationRequest request,
        string moderatorId,
        long tick,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(moderatorId))
            return ModerationResult.Reject("moderator_required", "A moderator id is required.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ModerationResult.Reject("reason_required", "A moderation reason is required.");

        var targetPlayerId = request.TargetPlayerId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(targetPlayerId) && request.Kind != ModerationActionKind.CloseVenue)
            return ModerationResult.Reject("target_required", "Choose a target player.");

        var report = FindReport(request.ReportId);
        if (request.ReportId is not null && report is null)
            return ModerationResult.Reject("report_not_found", "That report no longer exists.");

        if (report is not null &&
            !string.IsNullOrEmpty(targetPlayerId) &&
            !string.Equals(report.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
            return ModerationResult.Reject("report_mismatch", "The report targets a different player.");

        var id = Guid.NewGuid();
        var duration = request.DurationTicks > 0
            ? request.DurationTicks
            : request.Kind switch
            {
                ModerationActionKind.Mute => options.DefaultMuteTicks,
                ModerationActionKind.SuspendChat => options.DefaultSuspendChatTicks,
                _ => 0
            };
        string? counterparty = null;
        string? evidence = report?.Evidence;

        switch (request.Kind)
        {
            case ModerationActionKind.Warn:
                break;

            case ModerationActionKind.Mute:
                social.ApplyRestriction(new SocialRestriction(
                    id,
                    targetPlayerId,
                    tick + duration,
                    ChatBlocked: true,
                    ActivityBlocked: true,
                    InvitationsBlocked: true,
                    request.Reason,
                    tick));
                break;

            case ModerationActionKind.SuspendChat:
                social.ApplyRestriction(new SocialRestriction(
                    id,
                    targetPlayerId,
                    tick + duration,
                    ChatBlocked: true,
                    ActivityBlocked: false,
                    InvitationsBlocked: true,
                    request.Reason,
                    tick));
                break;

            case ModerationActionKind.Block:
                if (report is null)
                    return ModerationResult.Reject("report_required", "A block decision needs a report.");
                counterparty = report.ReporterPlayerId;
                var block = social.Block(targetPlayerId, counterparty, tick, SocialEdgeSource.Admin);
                if (!block.Accepted)
                    return ModerationResult.Reject(block.Code ?? "block_failed", "The block could not be applied.");
                break;

            case ModerationActionKind.RemoveMessage:
                if (request.MessageId is not { } messageId)
                    return ModerationResult.Reject("message_required", "Choose a message to remove.");
                if (report?.MessageId is { } reportedMessageId && reportedMessageId != messageId)
                    return ModerationResult.Reject("report_mismatch", "The report concerns a different message.");
                if (!await chat.RemoveAsync(messageId, cancellationToken))
                    return ModerationResult.Reject("message_not_found", "That message no longer exists.");
                break;

            case ModerationActionKind.CloseVenue:
                if (request.VenueNetworkId is not { } venueNetworkId)
                    return ModerationResult.Reject("venue_required", "Choose a venue to close.");
                var venues = engine.Venues;
                if (venues is null)
                    return ModerationResult.Reject("venue_unavailable", "Venues are not available.");
                var venue = venues.FindByNetworkId(venueNetworkId);
                if (venue == Entity.Null || !venues.SetClosed(venue, closed: true, tick))
                    return ModerationResult.Reject("venue_not_found", "That venue no longer exists.");
                break;

            default:
                return ModerationResult.Reject("invalid_action", "That action is not supported.");
        }

        var action = new ModerationAction
        {
            Id = id,
            ReportId = report?.Id,
            ModeratorId = moderatorId,
            TargetPlayerId = targetPlayerId,
            CounterpartyPlayerId = counterparty,
            Kind = request.Kind,
            DurationTicks = duration,
            CreatedTick = tick,
            EndsTick = duration > 0 ? tick + duration : null,
            Reason = request.Reason,
            Evidence = evidence,
            MessageId = request.MessageId,
            VenueNetworkId = request.VenueNetworkId
        };

        lock (gate)
            actions.Add(action);
        auditStore?.UpsertAction(action);
        if (report is not null)
            social.SetReportStatus(report.Id, ReportStatus.InReview);

        return new ModerationResult(true, null, "Moderation action applied.", action);
    }

    public ModerationResult Reverse(
        Guid actionId,
        string moderatorId,
        string reason,
        long tick)
    {
        if (string.IsNullOrWhiteSpace(moderatorId))
            return ModerationResult.Reject("moderator_required", "A moderator id is required.");
        if (string.IsNullOrWhiteSpace(reason))
            return ModerationResult.Reject("reason_required", "A reversal reason is required.");

        ModerationAction? original;
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            original = actions.FirstOrDefault(action => action.Id == actionId);
            if (original is null)
                return ModerationResult.Reject("action_not_found", "That moderation action no longer exists.");
            if (actions.Any(action => action.ReversesActionId == actionId))
                return ModerationResult.Reject("already_reversed", "That action was already reversed.");
        }

        ModerationActionKind inverse;
        switch (original.Kind)
        {
            case ModerationActionKind.Mute:
                social.RemoveRestriction(original.Id);
                inverse = ModerationActionKind.Unmute;
                break;
            case ModerationActionKind.SuspendChat:
                social.RemoveRestriction(original.Id);
                inverse = ModerationActionKind.ResumeChat;
                break;
            case ModerationActionKind.Block:
                if (string.IsNullOrEmpty(original.CounterpartyPlayerId))
                    return ModerationResult.Reject("invalid_action", "The original block has no counterparty.");
                social.Unblock(original.TargetPlayerId, original.CounterpartyPlayerId);
                inverse = ModerationActionKind.Unblock;
                break;
            case ModerationActionKind.CloseVenue:
                if (original.VenueNetworkId is not { } venueNetworkId)
                    return ModerationResult.Reject("invalid_action", "The original closure has no venue.");
                var venues = engine.Venues;
                if (venues is null)
                    return ModerationResult.Reject("venue_unavailable", "Venues are not available.");
                var venue = venues.FindByNetworkId(venueNetworkId);
                if (venue == Entity.Null || !venues.SetClosed(venue, closed: false, tick))
                    return ModerationResult.Reject("venue_not_found", "That venue no longer exists.");
                inverse = ModerationActionKind.ReopenVenue;
                break;
            default:
                return ModerationResult.Reject("not_reversible", "That action cannot be reversed.");
        }

        var reversal = new ModerationAction
        {
            Id = Guid.NewGuid(),
            ReportId = original.ReportId,
            ReversesActionId = original.Id,
            ModeratorId = moderatorId,
            TargetPlayerId = original.TargetPlayerId,
            CounterpartyPlayerId = original.CounterpartyPlayerId,
            Kind = inverse,
            CreatedTick = tick,
            Reason = reason,
            Evidence = original.Evidence,
            MessageId = original.MessageId,
            VenueNetworkId = original.VenueNetworkId
        };

        lock (gate)
            actions.Add(reversal);
        auditStore?.UpsertAction(reversal);
        if (original.ReportId is { } reportId)
            social.SetReportStatus(reportId, ReportStatus.Open);

        return new ModerationResult(true, null, "Moderation action reversed.", reversal);
    }

    public IReadOnlyList<ModerationAction> Actions(
        Guid? reportId = null,
        string? targetPlayerId = null,
        int take = 100)
    {
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            return actions
                .Where(action => reportId is null || action.ReportId == reportId)
                .Where(action => string.IsNullOrEmpty(targetPlayerId)
                    || string.Equals(action.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
                .OrderByDescending(action => action.CreatedTick)
                .Take(Math.Clamp(take, 1, 500))
                .ToArray();
        }
    }

    public IReadOnlyList<ModerationAction> Notices(string playerId, long tick)
    {
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            return actions
                .Where(action => string.Equals(action.TargetPlayerId, playerId, StringComparison.Ordinal))
                .Where(action => action.Kind is ModerationActionKind.Warn
                    or ModerationActionKind.Mute
                    or ModerationActionKind.SuspendChat
                    or ModerationActionKind.Unmute
                    or ModerationActionKind.ResumeChat)
                .Where(action => action.EndsTick is null || action.EndsTick > tick)
                .OrderByDescending(action => action.CreatedTick)
                .Take(50)
                .ToArray();
        }
    }

    public ModerationQueue Queue(long tick)
    {
        var reports = social.Reports()
            .Where(report => report.Status is ReportStatus.Open or ReportStatus.InReview)
            .ToArray();
        var windowStart = tick - options.ReportWindowTicks;
        var recent = social.Reports()
            .Where(report => report.CreatedTick >= windowStart)
            .ToArray();
        var byTarget = recent
            .GroupBy(report => report.TargetPlayerId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (Count: group.Count(), Reporters: group.Select(report => report.ReporterPlayerId).Distinct().Count()),
                StringComparer.Ordinal);

        var items = reports
            .OrderByDescending(report => report.CreatedTick)
            .Take(options.QueueTake)
            .Select(report =>
            {
                var stats = byTarget.TryGetValue(report.TargetPlayerId, out var found)
                    ? found
                    : (Count: 1, Reporters: 1);
                return new ModerationQueueItem(report, stats.Count, stats.Reporters);
            })
            .ToArray();

        return new ModerationQueue(items, RepeatOffenders(tick));
    }

    public IReadOnlyList<RepeatOffender> RepeatOffenders(long tick)
    {
        var windowStart = tick - options.ReportWindowTicks;
        return social.Reports()
            .Where(report => report.CreatedTick >= windowStart)
            .GroupBy(report => report.TargetPlayerId, StringComparer.Ordinal)
            .Select(group => new RepeatOffender(
                group.Key,
                group.Count(),
                group.Select(report => report.ReporterPlayerId).Distinct().Count(),
                group.Min(report => report.CreatedTick)))
            .Where(offender => offender.ReportCount >= options.RepeatOffenderThreshold)
            .OrderByDescending(offender => offender.ReportCount)
            .ThenBy(offender => offender.PlayerId, StringComparer.Ordinal)
            .ToArray();
    }

    public int PurgeExpired(long tick)
    {
        var removed = social.TrimReportsBefore(tick - options.ReportRetentionTicks);
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            removed += actions.RemoveAll(action => tick - action.CreatedTick > options.ActionRetentionTicks);
        }
        auditStore?.TrimActionsBefore(tick - options.ActionRetentionTicks);
        return removed;
    }

    private void EnsureAuditLoadedLocked()
    {
        if (auditLoaded || auditStore is null)
            return;

        auditLoaded = true;
        foreach (var action in auditStore.LoadActions())
        {
            if (actions.All(existing => existing.Id != action.Id))
                actions.Add(action);
        }
    }

    private SocialReport? FindReport(Guid? reportId) =>
        reportId is { } id
            ? social.Reports().FirstOrDefault(report => report.Id == id)
            : null;
}
