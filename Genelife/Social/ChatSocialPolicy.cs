namespace Genelife.Social;

/// <summary>
/// Consent and safety checks chat needs from the social graph. The graph is
/// keyed by player id and is implemented by <see cref="SocialGraphService"/>.
/// </summary>
public interface IChatSocialPolicy
{
    bool AreFriends(string firstPlayerId, string secondPlayerId);

    bool IsBlocked(string firstPlayerId, string secondPlayerId);

    bool IsMuted(string muterPlayerId, string authorPlayerId);
}
