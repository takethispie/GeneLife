using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Life;

namespace Genelife.Family;

public readonly record struct HeirAllocation(Entity Heir, decimal Share, string Tier);

public sealed class InheritancePlan
{
    public IReadOnlyList<HeirAllocation> Allocations { get; init; } = Array.Empty<HeirAllocation>();
}

/// <summary>
/// Relationship-based heir order: partner, children, parents/siblings, extended
/// family. Shares always describe the distributable remainder; the estate
/// settlement converts them to exact cents and escheats unallocated value.
/// </summary>
public static class HeirResolver
{
    public static InheritancePlan Resolve(
        World world,
        RelationshipIndex index,
        Entity deceased,
        InheritanceOptions? options = null,
        LifeStagePolicy? policy = null)
    {
        options ??= InheritanceOptions.Default;
        options.Validate();
        policy ??= LifeStagePolicy.Default;

        var partner = FirstLiving(world, index.ExPartnersOf(deceased)
            .Concat(index.PartnersOf(deceased)));
        var children = Living(world, index.ChildrenOf(deceased)).OrderBy(entity => NetworkIdOf(world, entity)).ToList();
        var parents = Living(world, index.ParentsOf(deceased)).OrderBy(entity => NetworkIdOf(world, entity)).ToList();
        var siblings = SiblingsOf(world, index, deceased).OrderBy(entity => NetworkIdOf(world, entity)).ToList();
        var extended = ExtendedFamily(world, index, deceased, options.ExtendedFamilyDepth)
            .OrderBy(entity => NetworkIdOf(world, entity)).ToList();

        var allocations = new List<HeirAllocation>();
        var remaining = 1m;

        if (partner != Entity.Null && options.PartnerShare > 0m)
        {
            var share = Math.Min(options.PartnerShare, remaining);
            allocations.Add(new HeirAllocation(partner, share, "partner"));
            remaining -= share;
        }

        if (children.Count > 0 && remaining > 0m)
        {
            var pool = partner != Entity.Null ? Math.Min(options.ChildSharePool, remaining) : remaining;
            if (pool > 0m)
            {
                var each = pool / children.Count;
                foreach (var child in children)
                    allocations.Add(new HeirAllocation(child, each, "child"));
                remaining -= pool;
            }
        }

        if (remaining > 0m)
        {
            List<(Entity Heir, string Tier)> tier;
            if (parents.Count > 0)
                tier = parents.Select(parent => (Heir: parent, Tier: "parent")).ToList();
            else if (siblings.Count > 0)
                tier = siblings.Select(sibling => (Heir: sibling, Tier: "sibling")).ToList();
            else
                tier = extended.Select(relative => (Heir: relative, Tier: "extended")).ToList();

            if (tier.Count > 0)
            {
                var each = remaining / tier.Count;
                foreach (var (heir, name) in tier)
                    allocations.Add(new HeirAllocation(heir, each, name));
                remaining = 0m;
            }
        }

        return new InheritancePlan { Allocations = allocations };
    }

    private static Entity FirstLiving(World world, IEnumerable<Entity> candidates)
    {
        foreach (var candidate in candidates.OrderBy(entity => NetworkIdOf(world, entity)))
            if (IsLiving(world, candidate))
                return candidate;
        return Entity.Null;
    }

    private static List<Entity> Living(World world, IEnumerable<Entity> candidates) =>
        candidates.Where(candidate => IsLiving(world, candidate)).Distinct().ToList();

    private static List<Entity> SiblingsOf(World world, RelationshipIndex index, Entity deceased)
    {
        var siblings = new HashSet<Entity>();
        foreach (var parent in index.ParentsOf(deceased))
            foreach (var child in index.ChildrenOf(parent))
                if (child != deceased)
                    siblings.Add(child);
        return siblings.Where(sibling => IsLiving(world, sibling)).ToList();
    }

    private static List<Entity> ExtendedFamily(
        World world,
        RelationshipIndex index,
        Entity deceased,
        int depth)
    {
        if (depth <= 0)
            return new List<Entity>();

        var relatives = new HashSet<Entity>();
        var grandparents = index.AncestorsOf(deceased, depth);
        foreach (var grandparent in grandparents)
        {
            relatives.Add(grandparent);
            foreach (var child in index.ChildrenOf(grandparent))
                if (child != deceased)
                    relatives.Add(child);
        }

        return relatives.Where(relative => IsLiving(world, relative)).ToList();
    }

    private static bool IsLiving(World world, Entity entity) =>
        entity != Entity.Null && world.IsAlive(entity) && !world.Has<Components.Gen.Dead>(entity);

    private static ulong NetworkIdOf(World world, Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
