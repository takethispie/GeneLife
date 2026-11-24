namespace Genelife.Social;

/// <summary>
/// In-memory social graph for friendships, blocks, mutes, friend requests and
/// reports. It is keyed by player id so it survives heir continuation. Schema
/// 15 persistence wires this snapshot into the world save in feature 04 step 07.
/// </summary>
public sealed class SocialGraphService : IChatSocialPolicy
{
    private readonly object gate = new();
    private readonly SocialGraphOptions options;
    private readonly ISocialAuditStore? auditStore;
    private readonly List<SocialEdge> edges = new();
    private readonly Dictionary<Guid, SocialRequest> requests = new();
    private readonly Dictionary<(string From, string To), long> declineCooldowns = new();
    private readonly List<SocialReport> reports = new();
    private readonly List<SocialRestriction> restrictions = new();
    private bool auditLoaded;

    public SocialGraphService(
        SocialGraphOptions? options = null,
        ISocialAuditStore? auditStore = null)
    {
        this.options = options ?? new SocialGraphOptions();
        this.options.Validate();
        this.auditStore = auditStore;
    }

    public SocialEdgeResult AddFriend(
        string firstPlayerId,
        string secondPlayerId,
        long tick,
        SocialEdgeSource source = SocialEdgeSource.Admin)
    {
        if (Same(firstPlayerId, secondPlayerId))
            return SocialEdgeResult.Reject("self_edge");

        lock (gate)
        {
            if (HasEdgeLocked(firstPlayerId, secondPlayerId, SocialEdgeKind.Friend)
                || HasEdgeLocked(secondPlayerId, firstPlayerId, SocialEdgeKind.Friend))
                return SocialEdgeResult.Reject("duplicate");

            if (IsBlockedLocked(firstPlayerId, secondPlayerId))
                return SocialEdgeResult.Reject("blocked");

            AddEdgeLocked(firstPlayerId, secondPlayerId, SocialEdgeKind.Friend, source, tick);
            AddEdgeLocked(secondPlayerId, firstPlayerId, SocialEdgeKind.Friend, source, tick);
            return SocialEdgeResult.Ok();
        }
    }

    public SocialEdgeResult RemoveFriend(string firstPlayerId, string secondPlayerId)
    {
        lock (gate)
        {
            var removed = RemoveEdgeLocked(firstPlayerId, secondPlayerId, SocialEdgeKind.Friend);
            removed |= RemoveEdgeLocked(secondPlayerId, firstPlayerId, SocialEdgeKind.Friend);
            return removed ? SocialEdgeResult.Ok() : SocialEdgeResult.Reject("not_found");
        }
    }

    /// <summary>
    /// Blocks are stored in both directions and cancel pending requests. The
    /// pair is excluded from presence, chat and future invitation flows.
    /// </summary>
    public SocialEdgeResult Block(
        string playerId,
        string blockedPlayerId,
        long tick,
        SocialEdgeSource source = SocialEdgeSource.Admin)
    {
        if (Same(playerId, blockedPlayerId))
            return SocialEdgeResult.Reject("self_edge");

        lock (gate)
        {
            if (IsBlockedLocked(playerId, blockedPlayerId))
                return SocialEdgeResult.Reject("duplicate");

            RemoveEdgeLocked(playerId, blockedPlayerId, SocialEdgeKind.Friend);
            RemoveEdgeLocked(blockedPlayerId, playerId, SocialEdgeKind.Friend);
            CancelPendingRequestsLocked(playerId, blockedPlayerId);

            AddEdgeLocked(playerId, blockedPlayerId, SocialEdgeKind.Block, source, tick);
            AddEdgeLocked(blockedPlayerId, playerId, SocialEdgeKind.Block, source, tick);
            return SocialEdgeResult.Ok();
        }
    }

    public SocialEdgeResult Unblock(string playerId, string blockedPlayerId)
    {
        lock (gate)
        {
            var removed = RemoveEdgeLocked(playerId, blockedPlayerId, SocialEdgeKind.Block);
            removed |= RemoveEdgeLocked(blockedPlayerId, playerId, SocialEdgeKind.Block);
            return removed ? SocialEdgeResult.Ok() : SocialEdgeResult.Reject("not_found");
        }
    }

    public SocialEdgeResult Mute(
        string muterPlayerId,
        string authorPlayerId,
        long tick,
        SocialEdgeSource source = SocialEdgeSource.Admin)
    {
        if (Same(muterPlayerId, authorPlayerId))
            return SocialEdgeResult.Reject("self_edge");

        lock (gate)
        {
            if (HasEdgeLocked(muterPlayerId, authorPlayerId, SocialEdgeKind.Mute))
                return SocialEdgeResult.Reject("duplicate");

            AddEdgeLocked(muterPlayerId, authorPlayerId, SocialEdgeKind.Mute, source, tick);
            return SocialEdgeResult.Ok();
        }
    }

    public SocialEdgeResult Unmute(string muterPlayerId, string authorPlayerId)
    {
        lock (gate)
        {
            return RemoveEdgeLocked(muterPlayerId, authorPlayerId, SocialEdgeKind.Mute)
                ? SocialEdgeResult.Ok()
                : SocialEdgeResult.Reject("not_found");
        }
    }

    public SocialRequestResult RequestFriend(
        string fromPlayerId,
        string toPlayerId,
        long tick)
    {
        if (Same(fromPlayerId, toPlayerId))
            return SocialRequestResult.Reject("self_edge");
        if (AreInvitationsBlocked(fromPlayerId, tick))
            return SocialRequestResult.Reject("suspended");

        lock (gate)
        {
            ExpireRequestsLocked(tick);
            if (IsBlockedLocked(fromPlayerId, toPlayerId))
                return SocialRequestResult.Reject("blocked");
            if (HasEdgeLocked(fromPlayerId, toPlayerId, SocialEdgeKind.Friend)
                || HasEdgeLocked(toPlayerId, fromPlayerId, SocialEdgeKind.Friend))
                return SocialRequestResult.Reject("already_friends");
            if (declineCooldowns.TryGetValue((fromPlayerId, toPlayerId), out var cooldownUntil)
                && tick < cooldownUntil)
                return SocialRequestResult.Reject("cooldown");

            var pending = requests.Values.FirstOrDefault(request =>
                request.Kind == SocialRequestKind.Friend
                && request.Status == SocialRequestStatus.Pending
                && ((Same(request.FromPlayerId, fromPlayerId) && Same(request.ToPlayerId, toPlayerId))
                    || (Same(request.FromPlayerId, toPlayerId) && Same(request.ToPlayerId, fromPlayerId))));
            if (pending is not null)
                return new SocialRequestResult(false, "request_pending", pending);

            var created = new SocialRequest
            {
                Id = Guid.NewGuid(),
                FromPlayerId = fromPlayerId,
                ToPlayerId = toPlayerId,
                Kind = SocialRequestKind.Friend,
                Status = SocialRequestStatus.Pending,
                CreatedTick = tick,
                ExpiresTick = tick + options.FriendRequestLifetimeTicks
            };
            requests[created.Id] = created;
            return new SocialRequestResult(true, null, created);
        }
    }

    public SocialRequestResult RespondToFriendRequest(
        Guid requestId,
        string actorPlayerId,
        bool accept,
        long tick)
    {
        lock (gate)
        {
            ExpireRequestsLocked(tick);
            if (!requests.TryGetValue(requestId, out var request))
                return SocialRequestResult.Reject("not_found");
            if (!Same(request.ToPlayerId, actorPlayerId))
                return SocialRequestResult.Reject("not_target");
            if (request.Status != SocialRequestStatus.Pending)
                return SocialRequestResult.Reject("not_pending");

            if (!accept)
            {
                var declined = request with { Status = SocialRequestStatus.Declined };
                requests[requestId] = declined;
                declineCooldowns[(request.FromPlayerId, request.ToPlayerId)] =
                    tick + options.FriendDeclineCooldownTicks;
                return new SocialRequestResult(true, null, declined);
            }

            if (IsBlockedLocked(request.FromPlayerId, request.ToPlayerId))
            {
                requests[requestId] = request with { Status = SocialRequestStatus.Cancelled };
                return SocialRequestResult.Reject("blocked");
            }

            AddEdgeLocked(
                request.FromPlayerId,
                request.ToPlayerId,
                SocialEdgeKind.Friend,
                SocialEdgeSource.Request,
                tick);
            AddEdgeLocked(
                request.ToPlayerId,
                request.FromPlayerId,
                SocialEdgeKind.Friend,
                SocialEdgeSource.Request,
                tick);

            var accepted = request with { Status = SocialRequestStatus.Accepted };
            requests[requestId] = accepted;
            return new SocialRequestResult(true, null, accepted);
        }
    }

    public SocialRequestResult CancelFriendRequest(Guid requestId, string actorPlayerId)
    {
        lock (gate)
        {
            if (!requests.TryGetValue(requestId, out var request))
                return SocialRequestResult.Reject("not_found");
            if (!Same(request.FromPlayerId, actorPlayerId) && !Same(request.ToPlayerId, actorPlayerId))
                return SocialRequestResult.Reject("not_participant");
            if (request.Status != SocialRequestStatus.Pending)
                return SocialRequestResult.Reject("not_pending");

            var cancelled = request with { Status = SocialRequestStatus.Cancelled };
            requests[requestId] = cancelled;
            return new SocialRequestResult(true, null, cancelled);
        }
    }

    public SocialReportResult Report(
        string reporterPlayerId,
        string targetPlayerId,
        ReportReason reason,
        Guid? messageId,
        string? evidence,
        long tick)
    {
        if (Same(reporterPlayerId, targetPlayerId))
            return SocialReportResult.Reject("self_report", "You cannot report yourself.");

        lock (gate)
        {
            EnsureAuditLoadedLocked();
            var existing = reports.LastOrDefault(report =>
                Same(report.ReporterPlayerId, reporterPlayerId)
                && Same(report.TargetPlayerId, targetPlayerId)
                && report.Reason == reason
                && report.MessageId == messageId
                && tick - report.CreatedTick <= options.ReportDedupeWindowTicks);
            if (existing is not null)
                return new SocialReportResult(true, null, null, existing);

            var report = new SocialReport
            {
                Id = Guid.NewGuid(),
                ReporterPlayerId = reporterPlayerId,
                TargetPlayerId = targetPlayerId,
                Reason = reason,
                MessageId = messageId,
                Evidence = evidence,
                CreatedTick = tick,
                Status = ReportStatus.Open
            };
            reports.Add(report);
            auditStore?.UpsertReport(report);
            return new SocialReportResult(true, null, null, report);
        }
    }

    public IReadOnlyList<SocialEdge> Friends(string playerId) =>
        EdgesFor(playerId, SocialEdgeKind.Friend);

    public IReadOnlyList<SocialEdge> Blocks(string playerId) =>
        EdgesFor(playerId, SocialEdgeKind.Block);

    public IReadOnlyList<SocialEdge> Mutes(string playerId) =>
        EdgesFor(playerId, SocialEdgeKind.Mute);

    public IReadOnlyList<SocialEdge> AdminEdges()
    {
        lock (gate)
            return edges.OrderBy(EdgeKey, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<SocialRequest> AdminRequests()
    {
        lock (gate)
            return requests.Values.OrderBy(request => request.CreatedTick).ToArray();
    }

    public IReadOnlyList<SocialReport> Reports(Guid? messageId = null)
    {
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            return reports
                .Where(report => messageId is null || report.MessageId == messageId)
                .OrderByDescending(report => report.CreatedTick)
                .ToArray();
        }
    }

    public void ApplyRestriction(SocialRestriction restriction)
    {
        ArgumentNullException.ThrowIfNull(restriction);
        lock (gate)
        {
            restrictions.RemoveAll(existing => existing.ActionId == restriction.ActionId);
            restrictions.Add(restriction);
        }
    }

    public bool RemoveRestriction(Guid actionId)
    {
        lock (gate)
            return restrictions.RemoveAll(restriction => restriction.ActionId == actionId) > 0;
    }

    public SocialRestriction? ActiveRestriction(string playerId, long tick)
    {
        lock (gate)
        {
            restrictions.RemoveAll(restriction => restriction.UntilTick <= tick);
            return restrictions
                .Where(restriction => Same(restriction.PlayerId, playerId))
                .OrderByDescending(restriction => restriction.CreatedTick)
                .FirstOrDefault();
        }
    }

    public bool IsChatBlocked(string playerId, long tick) =>
        AnyRestriction(playerId, tick, restriction => restriction.ChatBlocked);

    public bool IsActivityBlocked(string playerId, long tick) =>
        AnyRestriction(playerId, tick, restriction => restriction.ActivityBlocked);

    public bool AreInvitationsBlocked(string playerId, long tick) =>
        AnyRestriction(playerId, tick, restriction => restriction.InvitationsBlocked);

    public int TrimReportsBefore(long tick)
    {
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            var removed = reports.RemoveAll(report => report.CreatedTick < tick);
            auditStore?.TrimReportsBefore(tick);
            return removed;
        }
    }

    public bool SetReportStatus(Guid reportId, ReportStatus status)
    {
        lock (gate)
        {
            EnsureAuditLoadedLocked();
            var index = reports.FindIndex(report => report.Id == reportId);
            if (index < 0)
                return false;

            reports[index] = reports[index] with { Status = status };
            auditStore?.SetReportStatus(reportId, status);
            return true;
        }
    }

    public SocialOverview Overview(string playerId, long tick)
    {
        lock (gate)
        {
            ExpireRequestsLocked(tick);
            var friends = edges
                .Where(edge => edge.Kind == SocialEdgeKind.Friend && Same(edge.FromPlayerId, playerId))
                .OrderBy(edge => edge.ToPlayerId, StringComparer.Ordinal)
                .ToArray();
            var blocks = edges
                .Where(edge => edge.Kind == SocialEdgeKind.Block && Same(edge.FromPlayerId, playerId))
                .OrderBy(edge => edge.ToPlayerId, StringComparer.Ordinal)
                .ToArray();
            var mutes = edges
                .Where(edge => edge.Kind == SocialEdgeKind.Mute && Same(edge.FromPlayerId, playerId))
                .OrderBy(edge => edge.ToPlayerId, StringComparer.Ordinal)
                .ToArray();
            var incoming = requests.Values
                .Where(request => request.Status == SocialRequestStatus.Pending
                    && Same(request.ToPlayerId, playerId))
                .OrderBy(request => request.CreatedTick)
                .ToArray();
            var outgoing = requests.Values
                .Where(request => request.Status == SocialRequestStatus.Pending
                    && Same(request.FromPlayerId, playerId))
                .OrderBy(request => request.CreatedTick)
                .ToArray();

            return new SocialOverview(playerId, friends, blocks, mutes, incoming, outgoing);
        }
    }

    public SocialGraphSnapshot Export()
    {
        lock (gate)
            return new SocialGraphSnapshot(
                edges.OrderBy(EdgeKey, StringComparer.Ordinal).ToArray(),
                requests.Values.OrderBy(request => request.CreatedTick).ToArray(),
                reports.OrderBy(report => report.CreatedTick).ToArray())
            {
                Restrictions = restrictions.OrderBy(restriction => restriction.CreatedTick).ToArray()
            };
    }

    public void Restore(SocialGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (gate)
        {
            edges.Clear();
            requests.Clear();
            declineCooldowns.Clear();
            reports.Clear();
            restrictions.Clear();

            foreach (var edge in snapshot.Edges)
            {
                if (Same(edge.FromPlayerId, edge.ToPlayerId))
                    continue;
                if (HasEdgeLocked(edge.FromPlayerId, edge.ToPlayerId, edge.Kind))
                    continue;
                edges.Add(edge);
            }

            foreach (var request in snapshot.Requests)
                requests[request.Id] = request;

            reports.AddRange(snapshot.Reports);
            restrictions.AddRange(snapshot.Restrictions);
        }
    }

    public bool AreFriends(string firstPlayerId, string secondPlayerId)
    {
        lock (gate)
            return HasEdgeLocked(firstPlayerId, secondPlayerId, SocialEdgeKind.Friend)
                || HasEdgeLocked(secondPlayerId, firstPlayerId, SocialEdgeKind.Friend);
    }

    public bool IsBlocked(string firstPlayerId, string secondPlayerId)
    {
        lock (gate)
            return IsBlockedLocked(firstPlayerId, secondPlayerId);
    }

    public bool IsMuted(string muterPlayerId, string authorPlayerId)
    {
        lock (gate)
            return HasEdgeLocked(muterPlayerId, authorPlayerId, SocialEdgeKind.Mute);
    }

    private bool AnyRestriction(
        string playerId,
        long tick,
        Func<SocialRestriction, bool> predicate)
    {
        lock (gate)
        {
            restrictions.RemoveAll(restriction => restriction.UntilTick <= tick);
            return restrictions.Any(restriction =>
                Same(restriction.PlayerId, playerId) && predicate(restriction));
        }
    }

    private IReadOnlyList<SocialEdge> EdgesFor(string playerId, SocialEdgeKind kind)
    {
        lock (gate)
            return edges
                .Where(edge => edge.Kind == kind && Same(edge.FromPlayerId, playerId))
                .OrderBy(edge => edge.ToPlayerId, StringComparer.Ordinal)
                .ToArray();
    }

    private void EnsureAuditLoadedLocked()
    {
        if (auditLoaded || auditStore is null)
            return;

        auditLoaded = true;
        foreach (var report in auditStore.LoadReports())
        {
            if (reports.All(existing => existing.Id != report.Id))
                reports.Add(report);
        }
    }

    private bool IsBlockedLocked(string firstPlayerId, string secondPlayerId) =>
        HasEdgeLocked(firstPlayerId, secondPlayerId, SocialEdgeKind.Block)
        || HasEdgeLocked(secondPlayerId, firstPlayerId, SocialEdgeKind.Block);

    private bool HasEdgeLocked(string fromPlayerId, string toPlayerId, SocialEdgeKind kind) =>
        edges.Any(edge =>
            edge.Kind == kind
            && Same(edge.FromPlayerId, fromPlayerId)
            && Same(edge.ToPlayerId, toPlayerId));

    private void AddEdgeLocked(
        string fromPlayerId,
        string toPlayerId,
        SocialEdgeKind kind,
        SocialEdgeSource source,
        long tick)
    {
        edges.Add(new SocialEdge
        {
            FromPlayerId = fromPlayerId,
            ToPlayerId = toPlayerId,
            Kind = kind,
            SinceTick = tick,
            Source = source
        });
    }

    private bool RemoveEdgeLocked(string fromPlayerId, string toPlayerId, SocialEdgeKind kind) =>
        edges.RemoveAll(edge =>
            edge.Kind == kind
            && Same(edge.FromPlayerId, fromPlayerId)
            && Same(edge.ToPlayerId, toPlayerId)) > 0;

    private void CancelPendingRequestsLocked(string firstPlayerId, string secondPlayerId)
    {
        var ids = requests.Values
            .Where(request => request.Status == SocialRequestStatus.Pending
                && ((Same(request.FromPlayerId, firstPlayerId) && Same(request.ToPlayerId, secondPlayerId))
                    || (Same(request.FromPlayerId, secondPlayerId) && Same(request.ToPlayerId, firstPlayerId))))
            .Select(request => request.Id)
            .ToArray();

        foreach (var id in ids)
            requests[id] = requests[id] with { Status = SocialRequestStatus.Cancelled };
    }

    private void ExpireRequestsLocked(long tick)
    {
        var expired = requests.Values
            .Where(request => request.Status == SocialRequestStatus.Pending
                && request.ExpiresTick <= tick)
            .Select(request => request.Id)
            .ToArray();

        foreach (var id in expired)
            requests[id] = requests[id] with { Status = SocialRequestStatus.Expired };
    }

    private static string EdgeKey(SocialEdge edge) =>
        $"{edge.FromPlayerId}|{edge.ToPlayerId}|{(byte)edge.Kind}";

    private static bool Same(string first, string second) =>
        string.Equals(first, second, StringComparison.Ordinal);
}
