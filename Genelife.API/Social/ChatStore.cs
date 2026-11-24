using Genelife.Api.Models;
using Genelife.Social;
using Marten;

namespace Genelife.Api.Social;

/// <summary>
/// Marten-backed chat storage. Reads ignore expired and deleted messages, and
/// a retention sweep physically removes expired rows outside the tick loop.
/// </summary>
public sealed class MartenChatStore : IChatStore
{
    private readonly IDocumentStore store;

    public MartenChatStore(IDocumentStore store)
    {
        this.store = store;
    }

    public async Task SaveAsync(
        ChatMessage message,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();
        session.Store(new ChatMessageDocument
        {
            Id = message.Id,
            PlayerId = message.PlayerId,
            DisplayName = message.DisplayName,
            Channel = (byte)message.Channel,
            Text = message.Text,
            Tick = message.Tick,
            TargetPlayerId = message.TargetPlayerId,
            VenueNetworkId = message.VenueNetworkId,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        });
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChatMessage?> GetAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        var document = await session.LoadAsync<ChatMessageDocument>(messageId, cancellationToken);
        return document is null || document.DeletedAt is not null ? null : ToMessage(document);
    }

    public async Task<IReadOnlyList<ChatMessage>> QueryAsync(
        ChatQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.QuerySession();
        var channel = (byte)query.Channel;
        var now = query.Now;
        var messageQuery = session.Query<ChatMessageDocument>()
            .Where(document => document.Channel == channel)
            .Where(document => document.DeletedAt == null)
            .Where(document => document.ExpiresAt > now);

        if (query.InvolvingPlayerId is { } playerId)
            messageQuery = messageQuery.Where(document =>
                document.PlayerId == playerId || document.TargetPlayerId == playerId);

        if (query.VenueNetworkId is { } venueNetworkId)
            messageQuery = messageQuery.Where(document => document.VenueNetworkId == venueNetworkId);

        var take = Math.Clamp(query.Take, 1, 200);
        var documents = await messageQuery
            .OrderByDescending(document => document.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return documents.Select(ToMessage).ToArray();
    }

    public async Task<bool> RemoveAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();
        var document = await session.LoadAsync<ChatMessageDocument>(messageId, cancellationToken);
        if (document is null)
            return false;

        document.DeletedAt = DateTimeOffset.UtcNow;
        session.Store(document);
        await session.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();
        var cutoff = now;
        var expired = await session.Query<ChatMessageDocument>()
            .Where(document => document.ExpiresAt <= cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
            return 0;

        session.DeleteObjects(expired);
        await session.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    private static ChatMessage ToMessage(ChatMessageDocument document) => new()
    {
        Id = document.Id,
        PlayerId = document.PlayerId,
        DisplayName = document.DisplayName,
        Channel = (ChatChannel)document.Channel,
        Text = document.Text,
        Tick = document.Tick,
        TargetPlayerId = document.TargetPlayerId,
        VenueNetworkId = document.VenueNetworkId
    };
}

/// <summary>
/// Process-local chat storage used when Marten is disabled and by unit tests.
/// It applies the same expiry and deletion rules.
/// </summary>
public sealed class InMemoryChatStore : IChatStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, StoredMessage> messages = new();

    public Task SaveAsync(
        ChatMessage message,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
            messages[message.Id] = new StoredMessage(message, createdAt, expiresAt, null);
        return Task.CompletedTask;
    }

    public Task<ChatMessage?> GetAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return Task.FromResult(
                messages.TryGetValue(messageId, out var stored) && stored.DeletedAt is null
                    ? stored.Message
                    : null);
        }
    }

    public Task<IReadOnlyList<ChatMessage>> QueryAsync(
        ChatQuery query,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var result = messages.Values
                .Where(stored => stored.DeletedAt is null && stored.ExpiresAt > query.Now)
                .Where(stored => (byte)stored.Message.Channel == (byte)query.Channel)
                .Where(stored => query.InvolvingPlayerId is null
                    || string.Equals(stored.Message.PlayerId, query.InvolvingPlayerId, StringComparison.Ordinal)
                    || string.Equals(stored.Message.TargetPlayerId, query.InvolvingPlayerId, StringComparison.Ordinal))
                .Where(stored => query.VenueNetworkId is null
                    || stored.Message.VenueNetworkId == query.VenueNetworkId)
                .OrderByDescending(stored => stored.CreatedAt)
                .Take(Math.Clamp(query.Take, 1, 200))
                .Select(stored => stored.Message)
                .ToArray();
            return Task.FromResult<IReadOnlyList<ChatMessage>>(result);
        }
    }

    public Task<bool> RemoveAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (!messages.TryGetValue(messageId, out var stored))
                return Task.FromResult(false);

            messages[messageId] = stored with { DeletedAt = DateTimeOffset.UtcNow };
            return Task.FromResult(true);
        }
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var expired = messages
                .Where(pair => pair.Value.ExpiresAt <= now)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var id in expired)
                messages.Remove(id);
            return Task.FromResult(expired.Length);
        }
    }

    private sealed record StoredMessage(
        ChatMessage Message,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? DeletedAt);
}
