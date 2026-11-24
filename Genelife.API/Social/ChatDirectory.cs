using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Social;

namespace Genelife.Api.Social;

/// <summary>
/// Adapts live presence and the connection registry to the chat audience
/// contract. Presence is derived on demand and never enters the tick loop.
/// </summary>
public sealed class ChatDirectory : IChatDirectory
{
    private readonly SimulationEngine engine;
    private readonly IConnectionRegistry connections;
    private readonly SocialPrivacyRegistry privacy;

    public ChatDirectory(
        SimulationEngine engine,
        IConnectionRegistry connections,
        SocialPrivacyRegistry privacy)
    {
        this.engine = engine;
        this.connections = connections;
        this.privacy = privacy;
    }

    public ChatPlayerContext? Describe(string playerId)
    {
        var presence = engine.BuildPlayerPresence(playerId);
        return presence is null ? null : ToContext(presence);
    }

    public IReadOnlyList<ChatPlayerContext> Online() =>
        engine.BuildPlayerPresence(connections.GetPlayerIds())
            .Select(ToContext)
            .ToArray();

    private ChatPlayerContext ToContext(PlayerPresenceDto presence)
    {
        var settings = privacy.For(presence.PlayerId);
        var inVenue = presence.State == PresenceState.InVenue.ToString();
        return new ChatPlayerContext(
            presence.PlayerId,
            settings.ShowDisplayName ? presence.DisplayName : "Resident",
            presence.TileX,
            presence.TileY,
            inVenue ? presence.BuildingNetworkId : null,
            inVenue);
    }
}
