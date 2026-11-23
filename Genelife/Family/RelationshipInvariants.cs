using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;

namespace Genelife.Family;

public static class RelationshipInvariants
{
    public sealed record Issue(string Name, string Level, string Detail);

    public static IReadOnlyList<Issue> Verify(World world, RelationshipIndex index)
    {
        var issues = new List<Issue>();
        var componentCount = 0;
        var seen = new HashSet<Entity>();

        world.Query(in new QueryDescription().WithAll<Relationship>(),
            (Entity entity, ref Relationship relationship) =>
            {
                componentCount++;
                seen.Add(entity);

                if (relationship.From == relationship.To)
                    issues.Add(new Issue("relationship.self", "Fail",
                        $"Relationship {entity.Id} starts and ends at the same sim"));
                if (!world.IsAlive(relationship.From) || !world.IsAlive(relationship.To))
                    issues.Add(new Issue("relationship.endpoint", "Fail",
                        $"Relationship {entity.Id} has a destroyed endpoint"));
                if (!index.TryGetEdge(relationship.To, relationship.From,
                        RelationshipMirrors.MirrorOf(relationship.Kind), out _))
                    issues.Add(new Issue("relationship.mirror", "Fail",
                        $"Relationship {entity.Id} has no mirror edge"));

                switch (relationship.Kind)
                {
                    case RelationshipKind.Partner:
                        if (index.PartnersOf(relationship.From).Count > 1)
                            issues.Add(new Issue("partner.multiple", "Fail",
                                $"Sim {relationship.From.Id} has more than one partner"));
                        if (!index.PartnersOf(relationship.From).Contains(relationship.To))
                            issues.Add(new Issue("partner.index", "Fail",
                                $"Sim {relationship.From.Id} partner index is missing {relationship.To.Id}"));
                        break;
                    case RelationshipKind.Parent:
                        if (index.IsAncestorOf(relationship.To, relationship.From))
                            issues.Add(new Issue("ancestry.cycle", "Fail",
                                $"Parent edge {entity.Id} creates an ancestry cycle"));
                        break;
                    case RelationshipKind.Child:
                        if (index.IsAncestorOf(relationship.From, relationship.To))
                            issues.Add(new Issue("ancestry.cycle", "Fail",
                                $"Child edge {entity.Id} creates an ancestry cycle"));
                        break;
                    case RelationshipKind.Guardian:
                        if (index.IsAncestorOf(relationship.To, relationship.From))
                            issues.Add(new Issue("ancestry.cycle", "Fail",
                                $"Guardian edge {entity.Id} creates an ancestry cycle"));
                        break;
                    case RelationshipKind.Ward:
                        if (index.IsAncestorOf(relationship.From, relationship.To))
                            issues.Add(new Issue("ancestry.cycle", "Fail",
                                $"Ward edge {entity.Id} creates an ancestry cycle"));
                        break;
                }
            });

        if (componentCount != index.ComponentCount)
            issues.Add(new Issue("relationship.index-count", "Fail",
                $"World has {componentCount} relationship components but index has {index.ComponentCount}"));

        foreach (var edge in index.Edges)
        {
            if (!seen.Contains(edge))
                issues.Add(new Issue("relationship.stale-index", "Fail",
                    $"Index references missing relationship {edge.Id}"));
        }

        return issues;
    }
}
