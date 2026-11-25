using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Social;

namespace Genelife.Api.Social;

/// <summary>
/// Bounded presence scopes over the live player registry. Presence is derived
/// on demand from the world and connection registry; it never enters the tick
/// loop or the world snapshot.
/// </summary>
public sealed class PlayerDirectoryService
{
    private readonly SimulationEngine engine;
    private readonly IConnectionRegistry connections;
    private readonly SocialPrivacyRegistry privacy;
    private readonly SocialGraphService social;
    private readonly SocialRateLimitOptions limits;
    private readonly SocialRateLimiter presenceLimiter;

    public PlayerDirectoryService(
        SimulationEngine engine,
        IConnectionRegistry connections,
        SocialPrivacyRegistry privacy,
        SocialGraphService social,
        SocialRateLimitOptions? limits = null,
        TimeProvider? time = null)
    {
        this.engine = engine;
        this.connections = connections;
        this.privacy = privacy;
        this.social = social;
        this.limits = limits ?? new SocialRateLimitOptions();
        this.limits.Validate();
        presenceLimiter = new SocialRateLimiter(time ?? TimeProvider.System);
    }

    public PlayerDirectoryDto Directory(string callerId, PresenceScope scope, int radius = 12)
    {
        radius = Math.Clamp(radius, 1, 32);
        if (!presenceLimiter.TryAcquire("presence", callerId, limits.PresencePolicy, out var limitNotice))
            return new PlayerDirectoryDto(scope.ToString(), radius, Array.Empty<PlayerPresenceDto>())
            {
                Notice = limitNotice
            };

        var caller = engine.BuildPlayerPresence(callerId);
        if (caller is null)
            return new PlayerDirectoryDto(scope.ToString(), radius, Array.Empty<PlayerPresenceDto>());

        var online = connections.GetPlayerIds()
            .Where(playerId => !string.Equals(playerId, callerId, StringComparison.Ordinal))
            .ToArray();
        var rows = engine.BuildPlayerPresence(online)
            .Where(presence => Visible(caller, presence, scope, radius))
            .Select(presence => Sanitize(presence))
            .OrderBy(presence => Distance(caller, presence))
            .ThenBy(presence => presence.PlayerId, StringComparer.Ordinal)
            .ToList();

        return new PlayerDirectoryDto(scope.ToString(), radius, rows);
    }

    public IReadOnlyList<PlayerPresenceDto> AdminPresence()
    {
        var online = connections.GetPlayerIds();
        return engine.BuildPlayerPresence(online)
            .OrderBy(presence => presence.PlayerId, StringComparer.Ordinal)
            .ToList();
    }

    public SocialPrivacyDto GetPrivacy(string playerId)
    {
        var settings = privacy.For(playerId);
        return new SocialPrivacyDto(
            settings.ShowDisplayName,
            settings.AllowNearbyForNonFriends,
            settings.FriendsOnlyMessages);
    }

    public void SetPrivacy(string playerId, SocialPrivacyDto update) =>
        privacy.Set(playerId, new SocialPrivacy
        {
            ShowDisplayName = update.ShowDisplayName,
            AllowNearbyForNonFriends = update.AllowNearbyForNonFriends,
            FriendsOnlyMessages = update.FriendsOnlyMessages
        });

    private bool Visible(
        PlayerPresenceDto caller,
        PlayerPresenceDto other,
        PresenceScope scope,
        int radius)
    {
        if (social.IsBlocked(caller.PlayerId, other.PlayerId))
            return false;

        if (scope == PresenceScope.Venue)
            return caller.State == PresenceState.InVenue.ToString()
                && other.State == PresenceState.InVenue.ToString()
                && caller.BuildingNetworkId is not null
                && caller.BuildingNetworkId == other.BuildingNetworkId;

        if (scope == PresenceScope.Friend)
            return social.AreFriends(caller.PlayerId, other.PlayerId);

        if (!privacy.For(other.PlayerId).AllowNearbyForNonFriends)
            return false;

        return Distance(caller, other) <= radius;
    }

    private PlayerPresenceDto Sanitize(PlayerPresenceDto presence)
    {
        var settings = privacy.For(presence.PlayerId);
        return settings.ShowDisplayName
            ? presence
            : presence with { DisplayName = "Resident" };
    }

    private static int Distance(PlayerPresenceDto first, PlayerPresenceDto second) =>
        Math.Max(Math.Abs(first.TileX - second.TileX), Math.Abs(first.TileY - second.TileY));
}
