namespace Genelife.Api.Digest;

/// <summary>
/// A domain event reduced to its digest coordinates. The grouping key is
/// <c>(EventKind, SubjectEntityType, SubjectEntityId)</c> and the tick is the
/// simulation tick at which the event was observed — both computed by the
/// server.
/// </summary>
public readonly record struct DigestEvent(
    string EventKind,
    string SubjectEntityType,
    ulong SubjectEntityId,
    long Tick)
{
    /// <summary>Canonical <c>type:id</c> reference used to match a player's scope.</summary>
    public string SubjectRef => $"{SubjectEntityType}:{SubjectEntityId}";

    /// <summary>Stable grouping key, exactly as the GDD names it.</summary>
    public string GroupKey => $"{EventKind}\u0000{SubjectRef}";
}
