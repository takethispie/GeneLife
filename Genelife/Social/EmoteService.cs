using System.Collections.Concurrent;

namespace Genelife.Social;

public sealed record EmoteSendRequest
{
    public EmoteKind Emote { get; init; }
}

public sealed record EmoteSendResult(
    bool Accepted,
    string? Code,
    string? Notice,
    ChatEmote? Emote,
    IReadOnlyList<string> Recipients)
{
    public static EmoteSendResult Reject(string code, string notice) =>
        new(false, code, notice, null, Array.Empty<string>());
}

/// <summary>
/// Admission for predefined emotes. Emotes carry no payload, obey a per-player
/// cooldown and a per-venue budget, and are delivered publicly in the same
/// radius as <see cref="ChatChannel.Say"/>.
/// </summary>
public sealed class EmoteService
{
    private readonly IChatSocialPolicy social;
    private readonly IChatDirectory directory;
    private readonly ChatOptions options;
    private readonly TimeProvider time;
    private readonly ConcurrentDictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ChatService.RateWindow> venueBudgets = new(StringComparer.Ordinal);

    public EmoteService(
        IChatSocialPolicy social,
        IChatDirectory directory,
        ChatOptions options,
        TimeProvider time)
    {
        this.social = social;
        this.directory = directory;
        this.options = options;
        this.time = time;
        this.options.Validate();
    }

    public EmoteSendResult Send(string playerId, EmoteKind emote, long tick)
    {
        if (!Enum.IsDefined(emote))
            return EmoteSendResult.Reject("invalid_emote", "Unknown emote.");

        var sender = directory.Describe(playerId);
        if (sender is null)
            return EmoteSendResult.Reject("not_online", "Your character is not in the world.");
        if (social.IsChatBlocked(playerId, tick))
            return EmoteSendResult.Reject("chat_suspended", "Your chat is currently restricted.");

        var now = time.GetUtcNow();
        var cooldownKey = $"{playerId}|{(byte)emote}";
        if (cooldowns.TryGetValue(cooldownKey, out var last)
            && now - last < TimeSpan.FromSeconds(options.EmoteCooldownSeconds))
        {
            return EmoteSendResult.Reject(
                "emote_cooldown",
                "That emote is on cooldown. Try another gesture.");
        }

        var budgetKey = sender.VenueNetworkId is { } venue
            ? $"venue|{venue}"
            : $"player|{playerId}";
        var budget = venueBudgets.GetOrAdd(budgetKey, _ => new ChatService.RateWindow(DateTimeOffset.MinValue));
        lock (budget)
        {
            if (budget.Start == DateTimeOffset.MinValue
                || now - budget.Start >= TimeSpan.FromSeconds(options.EmoteVenueWindowSeconds))
            {
                budget.Start = now;
                budget.Count = 0;
            }

            if (budget.Count >= options.EmoteVenueBudget)
            {
                return EmoteSendResult.Reject(
                    "emote_budget",
                    "Too many emotes here right now. Try again shortly.");
            }

            budget.Count++;
        }

        cooldowns[cooldownKey] = now;

        var recipients = ChatAudience.Say(social, directory, sender, options.SayRadius);

        return new EmoteSendResult(
            true,
            null,
            null,
            new ChatEmote
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                DisplayName = sender.DisplayName,
                Emote = emote,
                Tick = tick,
                VenueNetworkId = sender.VenueNetworkId
            },
            recipients);
    }
}
