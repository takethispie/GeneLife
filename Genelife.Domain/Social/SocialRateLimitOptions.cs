namespace Genelife.Social;

public sealed class SocialRateLimitOptions
{
    public int PresencePerPlayer { get; set; } = 4;
    public int PresenceGlobal { get; set; } = 20;
    public int PresenceWindowSeconds { get; set; } = 10;

    public SocialRateLimitPolicy PresencePolicy => new(
        PresencePerPlayer,
        PresenceGlobal,
        PresenceWindowSeconds);

    public void Validate() => PresencePolicy.Validate();
}
