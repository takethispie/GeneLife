using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;

namespace Genelife.Family;

public enum RelationshipError
{
    None,
    SelfEdge,
    MissingSim,
    Duplicate,
    SecondPartner,
    AncestryCycle,
    UnsupportedKind
}

/// <summary>
/// Creates and removes relationship edges atomically. Edges and their mirrors
/// are components; the index is updated in the same apply phase.
/// </summary>
public sealed class RelationshipService
{
    private readonly World world;
    private readonly RelationshipIndex index;
    private ulong nextFallbackId = 2_000_000_000;

    public RelationshipService(World world, RelationshipIndex index)
    {
        this.world = world;
        this.index = index;
    }

    public Func<Entity, NetworkId>? NetworkIdAllocator { get; set; }
    public Action<Entity>? NetworkIdRemover { get; set; }
    public Action<Entity, Relationship>? EdgeCreated { get; set; }
    public Action<Entity, Relationship>? EdgeChanged { get; set; }

    public Entity CreatePair(
        Entity from,
        Entity to,
        RelationshipKind kind,
        RelationshipSource source,
        long tick) =>
        TryCreatePair(from, to, kind, source, tick, out var primary, out _, out var error)
            ? primary
            : throw new InvalidOperationException($"Cannot create relationship: {error}.");

    public bool TryCreatePair(
        Entity from,
        Entity to,
        RelationshipKind kind,
        RelationshipSource source,
        long tick,
        out Entity primary,
        out Entity mirror,
        out RelationshipError error)
    {
        primary = Entity.Null;
        mirror = Entity.Null;

        if (from == to)
        {
            error = RelationshipError.SelfEdge;
            return false;
        }

        if (!world.IsAlive(from) || !world.IsAlive(to))
        {
            error = RelationshipError.MissingSim;
            return false;
        }

        var mirrorKind = RelationshipMirrors.MirrorOf(kind);
        if (index.TryGetEdge(from, to, kind, out _) ||
            index.TryGetEdge(to, from, mirrorKind, out _))
        {
            error = RelationshipError.Duplicate;
            return false;
        }

        if (kind == RelationshipKind.Partner && (index.HasPartner(from) || index.HasPartner(to)))
        {
            error = RelationshipError.SecondPartner;
            return false;
        }

        if (!ValidateAncestry(from, to, kind, out error))
            return false;

        primary = CreateEdge(from, to, kind, source, tick);
        mirror = CreateEdge(to, from, mirrorKind, source, tick);
        error = RelationshipError.None;
        return true;
    }

    public bool EndPartnership(Entity first, Entity second, long tick)
    {
        if (!index.TryGetEdge(first, second, RelationshipKind.Partner, out var forward) ||
            !index.TryGetEdge(second, first, RelationshipKind.Partner, out var backward))
            return false;

        MarkExPartner(forward, tick);
        MarkExPartner(backward, tick);
        return true;
    }

    public bool RemoveEdge(Entity edge)
    {
        if (!world.IsAlive(edge) || !world.Has<Relationship>(edge))
            return false;

        var relationship = world.Get<Relationship>(edge);
        var mirrorKind = RelationshipMirrors.MirrorOf(relationship.Kind);
        if (index.TryGetEdge(relationship.To, relationship.From, mirrorKind, out var mirror))
        {
            RemoveSingle(mirror);
            RemoveSingle(edge);
        }
        else
        {
            RemoveSingle(edge);
        }

        return true;
    }

    public int RemoveEdgesForSim(Entity sim)
    {
        var edges = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Relationship>(),
            (Entity entity, ref Relationship relationship) =>
            {
                if (relationship.From == sim || relationship.To == sim)
                    edges.Add(entity);
            });

        var removed = 0;
        var seen = new HashSet<Entity>();
        foreach (var edge in edges)
        {
            if (!seen.Add(edge) || !world.IsAlive(edge))
                continue;

            var relationship = world.Get<Relationship>(edge);
            var mirrorKind = RelationshipMirrors.MirrorOf(relationship.Kind);
            if (index.TryGetEdge(relationship.To, relationship.From, mirrorKind, out var mirror) &&
                seen.Add(mirror))
            {
                RemoveSingle(mirror);
            }

            RemoveSingle(edge);
            removed++;
        }

        return removed;
    }

    private bool ValidateAncestry(Entity from, Entity to, RelationshipKind kind, out RelationshipError error)
    {
        error = RelationshipError.None;
        Entity parent;
        Entity child;

        switch (kind)
        {
            case RelationshipKind.Parent:
                parent = from;
                child = to;
                break;
            case RelationshipKind.Child:
                parent = to;
                child = from;
                break;
            case RelationshipKind.Guardian:
                if (index.IsAncestorOf(to, from))
                {
                    error = RelationshipError.AncestryCycle;
                    return false;
                }
                return true;
            case RelationshipKind.Ward:
                if (index.IsAncestorOf(from, to))
                {
                    error = RelationshipError.AncestryCycle;
                    return false;
                }
                return true;
            case RelationshipKind.Partner:
            case RelationshipKind.ExPartner:
                return true;
            default:
                error = RelationshipError.UnsupportedKind;
                return false;
        }

        if (parent == child || index.IsAncestorOf(child, parent))
        {
            error = RelationshipError.AncestryCycle;
            return false;
        }

        return true;
    }

    private Entity CreateEdge(
        Entity from,
        Entity to,
        RelationshipKind kind,
        RelationshipSource source,
        long tick)
    {
        var relationship = new Relationship
        {
            From = from,
            To = to,
            Kind = kind,
            SinceTick = tick,
            Source = source
        };

        var edge = world.Create(relationship);
        var networkId = NetworkIdAllocator?.Invoke(edge) ?? new NetworkId(nextFallbackId++);
        if (networkId.Value != 0 && !world.Has<NetworkId>(edge))
            world.Add(edge, networkId);

        index.OnCreated(edge, relationship);
        EdgeCreated?.Invoke(edge, relationship);
        return edge;
    }

    private void MarkExPartner(Entity edge, long tick)
    {
        var relationship = world.Get<Relationship>(edge);
        index.OnRemoved(edge, relationship);

        relationship.Kind = RelationshipKind.ExPartner;
        relationship.SinceTick = tick;
        world.Get<Relationship>(edge) = relationship;
        index.OnCreated(edge, relationship);
        EdgeChanged?.Invoke(edge, relationship);
    }

    private void RemoveSingle(Entity edge)
    {
        if (!world.IsAlive(edge) || !world.Has<Relationship>(edge))
            return;

        var relationship = world.Get<Relationship>(edge);
        index.OnRemoved(edge, relationship);
        if (world.IsAlive(edge))
            world.Destroy(edge);
        NetworkIdRemover?.Invoke(edge);
    }
}
