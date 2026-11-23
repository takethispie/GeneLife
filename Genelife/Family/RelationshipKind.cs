namespace Genelife.Family;

/// <summary>
/// Directional relationship kinds. Every edge has a mirror edge of the paired
/// kind on the other sim.
/// </summary>
public enum RelationshipKind : byte
{
    Parent,
    Child,
    Partner,
    ExPartner,
    Guardian,
    Ward
}

public enum RelationshipSource : byte
{
    Seed,
    Attraction,
    Admin,
    Migration,
    Death,
    Birth
}

public static class RelationshipMirrors
{
    public static RelationshipKind MirrorOf(RelationshipKind kind) => kind switch
    {
        RelationshipKind.Parent => RelationshipKind.Child,
        RelationshipKind.Child => RelationshipKind.Parent,
        RelationshipKind.Partner => RelationshipKind.Partner,
        RelationshipKind.ExPartner => RelationshipKind.ExPartner,
        RelationshipKind.Guardian => RelationshipKind.Ward,
        RelationshipKind.Ward => RelationshipKind.Guardian,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown relationship kind.")
    };

    public static bool IsActivePartner(RelationshipKind kind) => kind == RelationshipKind.Partner;

    public static bool IsAncestryKind(RelationshipKind kind) =>
        kind is RelationshipKind.Parent or RelationshipKind.Child or
            RelationshipKind.Guardian or RelationshipKind.Ward;
}
