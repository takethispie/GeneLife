using Arch.Core;

namespace Genelife.Components.Family;

using Genelife.Family;

/// <summary>
/// One directed relationship edge. Each edge is its own entity with a
/// NetworkId so persistence and the admin read model can reference it.
/// </summary>
public record struct Relationship
{
    public Entity From;
    public Entity To;
    public RelationshipKind Kind;
    public long SinceTick;
    public RelationshipSource Source;
}
