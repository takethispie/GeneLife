using System.Collections.Concurrent;

namespace Genelife.Social;

public sealed record ChatPlayerContext(
    string PlayerId,
    string? DisplayName,
    int TileX,
    int TileY,
    ulong? VenueNetworkId,
    bool InVenue);

/// <summary>
/// Read-only lookup used by chat and emotes. It is backed by the live presence
/// registry in the API and never by the tick loop.
/// </summary>
public interface IChatDirectory
{
    ChatPlayerContext? Describe(string playerId);

    IReadOnlyList<ChatPlayerContext> Online();
}

public sealed record ChatQuery(
    ChatChannel Channel,
    string? InvolvingPlayerId,
    ulong? VenueNetworkId,
    int Take,
    DateTimeOffset Now);

/// <summary>
/// Chat persistence. The Marten-backed implementation applies the retention
/// window; the in-memory implementation exists for tests and local runs
/// without Marten.
/// </summary>
public interface IChatStore
{
    Task SaveAsync(
        ChatMessage message,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<ChatMessage?> GetAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatMessage>> QueryAsync(
        ChatQuery query,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<ChatRetentionStatus> StatusAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed record ChatSendRequest
{
    public string Text { get; init; } = string.Empty;
    public ChatChannel Channel { get; init; } = ChatChannel.Say;
    public string? TargetPlayerId { get; init; }
}

public sealed record ChatSendResult(
    bool Accepted,
    string? Code,
    string? Notice,
    ChatMessage? Message,
    IReadOnlyList<string> Recipients)
{
    public static ChatSendResult Reject(string code, string notice) =>
        new(false, code, notice, null, Array.Empty<string>());
}

/// <summary>
/// Chat admission and delivery routing. It enforces length, channel and rate
/// limits, friendship/block checks for whispers, and mute filtering. It never
/// reads or mutates the ECS world, so chat allocations stay off the tick loop.
/// </summary>
public sealed class ChatService
{
    private readonly IChatStore store;
    private readonly SocialGraphService social;
    private readonly IChatDirectory directory;
    private readonly ChatOptions options;
    private readonly TimeProvider time;
    private readonly SocialRateLimiter limiter;

    public ChatService(
        IChatStore store,
        SocialGraphService social,
        IChatDirectory directory,
        ChatOptions options,
        TimeProvider time)
    {
        this.store = store;
        this.social = social;
        this.directory = directory;
        this.options = options;
        this.time = time;
        this.limiter = new SocialRateLimiter(time);
        this.options.Validate();
    }

    public async Task<ChatSendResult> SendAsync(
        string playerId,
        ChatSendRequest request,
        long tick,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Channel == ChatChannel.System)
            return ChatSendResult.Reject("system_channel", "System messages are server-authored.");

        var text = request.Text.Trim();
        if (text.Length == 0)
            return ChatSendResult.Reject("empty_message", "Write a message before sending.");
        if (text.Length > options.MaxLength)
            return ChatSendResult.Reject(
                "too_long",
                $"Messages are limited to {options.MaxLength} characters.");

        if (social.IsChatBlocked(playerId, tick))
            return ChatSendResult.Reject("chat_suspended", "Your chat is currently restricted.");

        if (!TryAcquireRateLimit(playerId, request.Channel, out var notice))
            return ChatSendResult.Reject("rate_limited", notice);

        var sender = directory.Describe(playerId);
        if (sender is null)
            return ChatSendResult.Reject("not_online", "Your character is not in the world.");

        IReadOnlyList<string> recipients;
        ChatPlayerContext? target = null;

        switch (request.Channel)
        {
            case ChatChannel.Say:
                recipients = ChatAudience.Say(social, directory, sender, options.SayRadius);
                break;

            case ChatChannel.Venue:
                if (!sender.InVenue || sender.VenueNetworkId is null)
                    return ChatSendResult.Reject("not_in_venue", "You are not inside a social venue.");
                recipients = ChatAudience.Venue(social, directory, sender);
                break;

            case ChatChannel.Whisper:
                if (string.IsNullOrWhiteSpace(request.TargetPlayerId))
                    return ChatSendResult.Reject("target_required", "Choose a friend to whisper.");
                if (string.Equals(request.TargetPlayerId, playerId, StringComparison.Ordinal))
                    return ChatSendResult.Reject("self_target", "You cannot whisper yourself.");

                target = directory.Describe(request.TargetPlayerId);
                if (target is null)
                    return ChatSendResult.Reject("target_not_found", "That player is not available.");
                if (social.IsBlocked(playerId, target.PlayerId))
                    return ChatSendResult.Reject("blocked", "You cannot message that player.");
                if (!social.AreFriends(playerId, target.PlayerId))
                    return ChatSendResult.Reject("not_friends", "Whispers require friendship.");

                recipients = social.IsMuted(target.PlayerId, playerId)
                    ? Array.Empty<string>()
                    : new[] { target.PlayerId };
                break;

            default:
                return ChatSendResult.Reject("invalid_channel", "Unknown chat channel.");
        }

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            DisplayName = sender.DisplayName,
            Channel = request.Channel,
            Text = text,
            Tick = tick,
            TargetPlayerId = target?.PlayerId,
            VenueNetworkId = sender.VenueNetworkId
        };

        var now = time.GetUtcNow();
        await store.SaveAsync(message, now, now.AddDays(options.RetentionDays), cancellationToken);

        return new ChatSendResult(true, null, null, message, recipients);
    }

    public async Task<IReadOnlyList<ChatMessage>> ReadAsync(
        string playerId,
        ChatChannel channel,
        string? targetPlayerId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var reader = directory.Describe(playerId);
        if (reader is null)
            return Array.Empty<ChatMessage>();

        if (channel == ChatChannel.Whisper)
        {
            var whispers = await store.QueryAsync(
                new ChatQuery(channel, playerId, null, 200, time.GetUtcNow()),
                cancellationToken);

            return whispers
                .Where(message => targetPlayerId is null
                    ? string.Equals(message.PlayerId, playerId, StringComparison.Ordinal)
                        || string.Equals(message.TargetPlayerId, playerId, StringComparison.Ordinal)
                    : (string.Equals(message.PlayerId, playerId, StringComparison.Ordinal)
                       && string.Equals(message.TargetPlayerId, targetPlayerId, StringComparison.Ordinal))
                        || (string.Equals(message.PlayerId, targetPlayerId, StringComparison.Ordinal)
                            && string.Equals(message.TargetPlayerId, playerId, StringComparison.Ordinal)))
                .Where(message => !social.IsBlocked(playerId, message.PlayerId))
                .Where(message => !social.IsMuted(playerId, message.PlayerId))
                .OrderByDescending(message => message.Tick)
                .Take(Math.Clamp(take, 1, 200))
                .OrderBy(message => message.Tick)
                .ToArray();
        }

        if (channel == ChatChannel.Venue && (!reader.InVenue || reader.VenueNetworkId is null))
            return Array.Empty<ChatMessage>();

        var rows = await store.QueryAsync(
            new ChatQuery(
                channel,
                null,
                channel == ChatChannel.Venue ? reader.VenueNetworkId : null,
                Math.Clamp(take * 4, 1, 200),
                time.GetUtcNow()),
            cancellationToken);

        return rows
            .Where(message => channel switch
            {
                ChatChannel.System => true,
                ChatChannel.Venue => reader.InVenue
                    && reader.VenueNetworkId is not null
                    && message.VenueNetworkId == reader.VenueNetworkId,
                _ => SameVenue(reader, message.VenueNetworkId)
                    || IsNearby(reader, message.PlayerId)
            })
            .Where(message => !social.IsBlocked(playerId, message.PlayerId))
            .Where(message => !social.IsMuted(playerId, message.PlayerId))
            .OrderByDescending(message => message.Tick)
            .Take(Math.Clamp(take, 1, 200))
            .OrderBy(message => message.Tick)
            .ToArray();
    }

    public Task<ChatMessage?> GetMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default) =>
        store.GetAsync(messageId, cancellationToken);

    public async Task<SocialReportResult> ReportAsync(
        string reporterPlayerId,
        Guid messageId,
        ReportReason reason,
        long tick,
        CancellationToken cancellationToken = default)
    {
        var message = await store.GetAsync(messageId, cancellationToken);
        if (message is null)
            return SocialReportResult.Reject("message_not_found", "That message no longer exists.");

        return social.Report(
            reporterPlayerId,
            message.PlayerId,
            reason,
            message.Id,
            message.Text,
            tick);
    }

    public async Task<bool> RemoveAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        await store.RemoveAsync(messageId, cancellationToken);

    public Task<ChatRetentionStatus> StatusAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        store.StatusAsync(now, cancellationToken);

    private bool TryAcquireRateLimit(string playerId, ChatChannel channel, out string notice)
    {
        var policy = channel switch
        {
            ChatChannel.Say => new SocialRateLimitPolicy(
                options.SayPerWindow,
                options.GlobalSayPerWindow,
                options.RateWindowSeconds),
            ChatChannel.Venue => new SocialRateLimitPolicy(
                options.VenuePerWindow,
                options.GlobalVenuePerWindow,
                options.RateWindowSeconds),
            ChatChannel.Whisper => new SocialRateLimitPolicy(
                options.WhisperPerWindow,
                options.GlobalWhisperPerWindow,
                options.RateWindowSeconds),
            _ => SocialRateLimitPolicy.Disabled
        };

        return limiter.TryAcquire($"chat:{(byte)channel}", playerId, policy, out notice);
    }

    private bool IsNearby(ChatPlayerContext reader, string authorPlayerId)
    {
        if (string.Equals(reader.PlayerId, authorPlayerId, StringComparison.Ordinal))
            return true;

        var author = directory.Describe(authorPlayerId);
        return author is not null && Distance(reader, author) <= options.SayRadius;
    }

    private static bool SameVenue(ChatPlayerContext reader, ulong? venueNetworkId) =>
        reader.InVenue && reader.VenueNetworkId is not null && reader.VenueNetworkId == venueNetworkId;

    private static int Distance(ChatPlayerContext first, ChatPlayerContext second) =>
        Math.Max(Math.Abs(first.TileX - second.TileX), Math.Abs(first.TileY - second.TileY));

    internal sealed class RateWindow
    {
        public RateWindow(DateTimeOffset start) => Start = start;

        public DateTimeOffset Start;
        public int Count;
    }
}

internal static class ChatAudience
{
    public static IReadOnlyList<string> Say(
        IChatSocialPolicy social,
        IChatDirectory directory,
        ChatPlayerContext sender,
        int radius) =>
        directory.Online()
            .Where(other => !SamePlayer(other, sender))
            .Where(other => SameVenue(sender, other) || Distance(sender, other) <= radius)
            .Where(other => !social.IsBlocked(sender.PlayerId, other.PlayerId))
            .Where(other => !social.IsMuted(other.PlayerId, sender.PlayerId))
            .Select(other => other.PlayerId)
            .ToArray();

    public static IReadOnlyList<string> Venue(
        IChatSocialPolicy social,
        IChatDirectory directory,
        ChatPlayerContext sender)
    {
        if (sender.VenueNetworkId is null)
            return Array.Empty<string>();

        return directory.Online()
            .Where(other => !SamePlayer(other, sender))
            .Where(other => SameVenue(sender, other))
            .Where(other => !social.IsBlocked(sender.PlayerId, other.PlayerId))
            .Where(other => !social.IsMuted(other.PlayerId, sender.PlayerId))
            .Select(other => other.PlayerId)
            .ToArray();
    }

    private static bool SamePlayer(ChatPlayerContext first, ChatPlayerContext second) =>
        string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal);

    private static bool SameVenue(ChatPlayerContext first, ChatPlayerContext second) =>
        first.InVenue
        && second.InVenue
        && first.VenueNetworkId is not null
        && first.VenueNetworkId == second.VenueNetworkId;

    private static int Distance(ChatPlayerContext first, ChatPlayerContext second) =>
        Math.Max(Math.Abs(first.TileX - second.TileX), Math.Abs(first.TileY - second.TileY));
}
