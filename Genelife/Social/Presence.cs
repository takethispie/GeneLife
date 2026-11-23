namespace Genelife.Social;

public enum PresenceState : byte { Online, Afk, InVenue, Offline }

public enum PresenceScope : byte { Nearby, Venue, Friend }

/// <summary>
/// Privacy-safe public presence record. It deliberately contains no wallet,
/// family, trait, job, health or utility fields.
/// </summary>
public sealed record PublicPresence(
    string PlayerId,
    string? DisplayName,
    ulong SimNetworkId,
    int TileX,
    int TileY,
    ulong? BuildingNetworkId,
    PresenceState State,
    long LastSeenTick);

public sealed class SocialPrivacy
{
    public bool ShowDisplayName { get; set; } = true;
    public bool AllowNearbyForNonFriends { get; set; } = true;
}

/// <summary>Process-local privacy settings; persistence is a feature 04 P07 task.</summary>
public sealed class SocialPrivacyRegistry
{
    private readonly Dictionary<string, SocialPrivacy> settings = new(StringComparer.Ordinal);

    public SocialPrivacy For(string playerId)
    {
        if (!settings.TryGetValue(playerId, out var privacy))
        {
            privacy = new SocialPrivacy();
            settings[playerId] = privacy;
        }
        return privacy;
    }

    public void Set(string playerId, SocialPrivacy privacy) => settings[playerId] = privacy;
}
