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
    ulong GenosNetworkId,
    int TileX,
    int TileY,
    ulong? BuildingNetworkId,
    PresenceState State,
    long LastSeenTick);

public sealed class SocialPrivacy
{
    public bool ShowDisplayName { get; set; } = true;
    public bool AllowNearbyForNonFriends { get; set; } = true;
    public bool FriendsOnlyMessages { get; set; }
}

public sealed record SocialPrivacySetting(
    string PlayerId,
    bool ShowDisplayName,
    bool AllowNearbyForNonFriends,
    bool FriendsOnlyMessages);

/// <summary>
/// Player privacy settings, persisted with schema 15. Display names are derived
/// from the genos and never stored as real names or external identifiers.
/// </summary>
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

    public IReadOnlyList<SocialPrivacySetting> Export() =>
        settings
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new SocialPrivacySetting(
                pair.Key,
                pair.Value.ShowDisplayName,
                pair.Value.AllowNearbyForNonFriends,
                pair.Value.FriendsOnlyMessages))
            .ToArray();

    public void Restore(IEnumerable<SocialPrivacySetting> restored)
    {
        settings.Clear();
        foreach (var setting in restored)
        {
            settings[setting.PlayerId] = new SocialPrivacy
            {
                ShowDisplayName = setting.ShowDisplayName,
                AllowNearbyForNonFriends = setting.AllowNearbyForNonFriends,
                FriendsOnlyMessages = setting.FriendsOnlyMessages
            };
        }
    }
}
