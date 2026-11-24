namespace Genelife.Social;

public sealed class SocialGraphOptions
{
    public int FriendRequestLifetimeTicks { get; set; } = 100;
    public int FriendDeclineCooldownTicks { get; set; } = 600;
    public int ReportDedupeWindowTicks { get; set; } = 600;

    public void Validate()
    {
        if (FriendRequestLifetimeTicks < 1)
            throw new InvalidOperationException("FriendRequestLifetimeTicks must be positive.");
        if (FriendDeclineCooldownTicks < 0)
            throw new InvalidOperationException("FriendDeclineCooldownTicks cannot be negative.");
        if (ReportDedupeWindowTicks < 0)
            throw new InvalidOperationException("ReportDedupeWindowTicks cannot be negative.");
    }
}
