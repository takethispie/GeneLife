using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;

namespace Genelife.Family;

/// <summary>
/// Derived index over relationship edges. Components are authoritative; the
/// index is rebuilt on restore and updated only in apply phases.
/// </summary>
public sealed class RelationshipIndex
{
    private readonly Dictionary<(Entity From, Entity To, RelationshipKind Kind), Entity> edgeByKey = new();
    private readonly HashSet<Entity> edgeEntities = new();

    public RelationshipIndex()
    {
        NetworkIdResolver = entity => 0;
    }

    public Dictionary<Entity, List<Entity>> PartnersBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> ParentsBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> ChildrenBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> GuardiansBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> WardsBySim { get; } = new();
    public Dictionary<Entity, List<Entity>> ExPartnersBySim { get; } = new();

    /// <summary>Stable ordering source for all lists. Set by the engine on restore.</summary>
    public Func<Entity, ulong> NetworkIdResolver { get; set; }

    public int ComponentCount => edgeEntities.Count;
    public int RelationshipCount => edgeByKey.Count;
    public IReadOnlyCollection<Entity> Edges => edgeEntities;

    public void Clear()
    {
        PartnersBySim.Clear();
        ParentsBySim.Clear();
        ChildrenBySim.Clear();
        GuardiansBySim.Clear();
        WardsBySim.Clear();
        ExPartnersBySim.Clear();
        edgeByKey.Clear();
        edgeEntities.Clear();
    }

    public void Rebuild(World world)
    {
        Clear();
        NetworkIdResolver = entity =>
            world.IsAlive(entity) && world.Has<NetworkId>(entity)
                ? world.Get<NetworkId>(entity).Value
                : (ulong)entity.Id;

        var edges = new List<(Entity Edge, Relationship Relationship, ulong NetworkId)>();
        world.Query(in new QueryDescription().WithAll<Relationship>(),
            (Entity entity, ref Relationship relationship) =>
            {
                var networkId = world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
                edges.Add((entity, relationship, networkId));
            });

        edges.Sort((a, b) =>
        {
            var byNetworkId = a.NetworkId.CompareTo(b.NetworkId);
            return byNetworkId != 0 ? byNetworkId : a.Edge.Id.CompareTo(b.Edge.Id);
        });

        foreach (var (edge, relationship, _) in edges)
            OnCreated(edge, relationship);
    }

    public void OnCreated(Entity edge, in Relationship relationship)
    {
        if (relationship.From == relationship.To)
            return;

        edgeEntities.Add(edge);
        edgeByKey[(relationship.From, relationship.To, relationship.Kind)] = edge;

        switch (relationship.Kind)
        {
            case RelationshipKind.Partner:
                AddSorted(PartnersBySim, relationship.From, relationship.To);
                break;
            case RelationshipKind.ExPartner:
                AddSorted(ExPartnersBySim, relationship.From, relationship.To);
                break;
            case RelationshipKind.Parent:
                AddSorted(ChildrenBySim, relationship.From, relationship.To);
                AddSorted(ParentsBySim, relationship.To, relationship.From);
                break;
            case RelationshipKind.Child:
                AddSorted(ParentsBySim, relationship.From, relationship.To);
                AddSorted(ChildrenBySim, relationship.To, relationship.From);
                break;
            case RelationshipKind.Guardian:
                AddSorted(WardsBySim, relationship.From, relationship.To);
                AddSorted(GuardiansBySim, relationship.To, relationship.From);
                break;
            case RelationshipKind.Ward:
                AddSorted(GuardiansBySim, relationship.From, relationship.To);
                AddSorted(WardsBySim, relationship.To, relationship.From);
                break;
        }
    }

    public void OnRemoved(Entity edge, in Relationship relationship)
    {
        edgeEntities.Remove(edge);
        edgeByKey.Remove((relationship.From, relationship.To, relationship.Kind));

        switch (relationship.Kind)
        {
            case RelationshipKind.Partner:
                Remove(PartnersBySim, relationship.From, relationship.To);
                break;
            case RelationshipKind.ExPartner:
                Remove(ExPartnersBySim, relationship.From, relationship.To);
                break;
            case RelationshipKind.Parent:
                Remove(ChildrenBySim, relationship.From, relationship.To);
                Remove(ParentsBySim, relationship.To, relationship.From);
                break;
            case RelationshipKind.Child:
                Remove(ParentsBySim, relationship.From, relationship.To);
                Remove(ChildrenBySim, relationship.To, relationship.From);
                break;
            case RelationshipKind.Guardian:
                Remove(WardsBySim, relationship.From, relationship.To);
                Remove(GuardiansBySim, relationship.To, relationship.From);
                break;
            case RelationshipKind.Ward:
                Remove(GuardiansBySim, relationship.From, relationship.To);
                Remove(WardsBySim, relationship.To, relationship.From);
                break;
        }
    }

    public bool TryGetEdge(Entity from, Entity to, RelationshipKind kind, out Entity edge) =>
        edgeByKey.TryGetValue((from, to, kind), out edge);

    public IReadOnlyList<Entity> PartnersOf(Entity sim) => Get(PartnersBySim, sim);
    public IReadOnlyList<Entity> ParentsOf(Entity sim) => Get(ParentsBySim, sim);
    public IReadOnlyList<Entity> ChildrenOf(Entity sim) => Get(ChildrenBySim, sim);
    public IReadOnlyList<Entity> GuardiansOf(Entity sim) => Get(GuardiansBySim, sim);
    public IReadOnlyList<Entity> WardsOf(Entity sim) => Get(WardsBySim, sim);
    public IReadOnlyList<Entity> ExPartnersOf(Entity sim) => Get(ExPartnersBySim, sim);

    public bool HasPartner(Entity sim) => PartnersOf(sim).Count > 0;

    public bool IsParentOf(Entity parent, Entity child) =>
        ChildrenOf(parent).Contains(child);

    public bool IsGuardianOf(Entity guardian, Entity ward) =>
        WardsOf(guardian).Contains(ward);

    public bool AreSiblings(Entity a, Entity b)
    {
        if (a == b)
            return false;
        var parentsA = ParentsOf(a);
        if (parentsA.Count == 0)
            return false;
        foreach (var parent in parentsA)
            if (ParentsOf(b).Contains(parent))
                return true;
        return false;
    }

    public HashSet<Entity> AncestorsOf(Entity sim, int maxDepth)
    {
        var result = new HashSet<Entity>();
        if (maxDepth <= 0)
            return result;

        var frontier = new List<Entity> { sim };
        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var next = new List<Entity>();
            foreach (var current in frontier)
            {
                foreach (var parent in ParentsOf(current))
                {
                    if (parent == sim || !result.Add(parent))
                        continue;
                    next.Add(parent);
                }
            }
            frontier = next;
        }

        return result;
    }

    public bool IsAncestorOf(Entity ancestor, Entity descendant) =>
        ancestor != descendant && AncestorsOf(descendant, int.MaxValue).Contains(ancestor);

    public bool AreFirstCousins(Entity a, Entity b)
    {
        if (a == b || AreSiblings(a, b) || IsAncestorOf(a, b) || IsAncestorOf(b, a))
            return false;

        var ancestorsA = AncestorsOf(a, 2);
        if (ancestorsA.Count == 0)
            return false;

        foreach (var ancestor in AncestorsOf(b, 2))
            if (ancestorsA.Contains(ancestor))
                return true;

        return false;
    }

    public bool AreAuntUncle(Entity a, Entity b)
    {
        if (a == b)
            return false;

        foreach (var parent in ParentsOf(b))
            if (AreSiblings(a, parent))
                return true;
        foreach (var parent in ParentsOf(a))
            if (AreSiblings(b, parent))
                return true;
        return false;
    }

    public bool HasCommonAncestorWithin(Entity a, Entity b, int maxDepth)
    {
        if (a == b)
            return true;
        var ancestorsA = AncestorsOf(a, maxDepth);
        if (ancestorsA.Count == 0)
            return false;
        foreach (var ancestor in AncestorsOf(b, maxDepth))
            if (ancestorsA.Contains(ancestor))
                return true;
        return false;
    }

    private static IReadOnlyList<Entity> Get(Dictionary<Entity, List<Entity>> map, Entity sim) =>
        map.TryGetValue(sim, out var list) ? list : Array.Empty<Entity>();

    private void AddSorted(Dictionary<Entity, List<Entity>> map, Entity from, Entity to)
    {
        if (!map.TryGetValue(from, out var list))
        {
            list = new List<Entity>(2);
            map[from] = list;
        }

        if (list.Contains(to))
            return;

        var key = NetworkIdResolver(to);
        var index = list.FindIndex(existing =>
        {
            var existingKey = NetworkIdResolver(existing);
            return existingKey > key || (existingKey == key && existing.Id > to.Id);
        });

        if (index < 0)
            list.Add(to);
        else
            list.Insert(index, to);
    }

    private static void Remove(Dictionary<Entity, List<Entity>> map, Entity from, Entity to)
    {
        if (!map.TryGetValue(from, out var list))
            return;
        list.Remove(to);
        if (list.Count == 0)
            map.Remove(from);
    }
}
