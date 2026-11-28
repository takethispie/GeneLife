namespace Genelife.Api.Digest;

/// <summary>
/// The set of entity references a player is connected to — the genoses they
/// control and their household, workplace, family and venue — expressed as
/// canonical <c>type:id</c> refs. The digest keeps only events whose subject is
/// in this set.
/// </summary>
public sealed class PlayerDigestScope
{
    public PlayerDigestScope(string playerId, ulong genosNetworkId, IEnumerable<string> entityRefs)
    {
        PlayerId = playerId;
        GenosNetworkId = genosNetworkId;
        EntityRefs = new HashSet<string>(entityRefs, StringComparer.Ordinal);
    }

    public string PlayerId { get; }

    public ulong GenosNetworkId { get; }

    public IReadOnlySet<string> EntityRefs { get; }

    public bool Contains(string entityType, ulong entityId) =>
        EntityRefs.Contains($"{entityType}:{entityId}");
}
