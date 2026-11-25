using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;

namespace Genelife.Family;

public readonly record struct SeedPair(Entity First, Entity Second);

/// <summary>
/// Deterministic seeded family construction. It creates partner edges first so
/// households can be formed directly around pairs, then adds parent edges for
/// assigned children.
/// </summary>
public sealed class SeedFamilyBuilder
{
    private readonly World world;
    private readonly RelationshipIndex index;
    private readonly RelationshipService service;
    private readonly RelationshipOptions options;

    public SeedFamilyBuilder(
        World world,
        RelationshipIndex index,
        RelationshipService service,
        RelationshipOptions? options = null)
    {
        this.world = world;
        this.index = index;
        this.service = service;
        this.options = options ?? RelationshipOptions.Default;
        this.options.Validate();
        index.NetworkIdResolver = NetworkIdOf;
    }

    public List<SeedPair> BuildPairs(IReadOnlyList<Entity> adults, long tick)
    {
        if (options.SeedPartnershipShare <= 0m)
            return [];

        var eligible = adults
            .Where(adult => world.IsAlive(adult) && world.Has<Alive>(adult) && !world.Has<Dead>(adult))
            .Distinct()
            .OrderBy(NetworkIdOf)
            .ThenBy(adult => adult.Id)
            .ToList();

        if (eligible.Count < 2)
            return [];

        var targetPairs = options.SeedPartnershipShare >= 1m
            ? eligible.Count / 2
            : (int)Math.Floor(eligible.Count * options.SeedPartnershipShare / 2m);
        if (targetPairs <= 0)
            return [];

        var paired = new HashSet<Entity>();
        var pairs = new List<SeedPair>(targetPairs);

        foreach (var genos in eligible)
        {
            if (pairs.Count >= targetPairs)
                break;
            if (paired.Contains(genos))
                continue;

            var partner = FindPartner(genos, eligible, paired);
            if (partner == Entity.Null)
                continue;

            if (!service.TryCreatePair(
                    genos,
                    partner,
                    RelationshipKind.Partner,
                    RelationshipSource.Seed,
                    tick,
                    out _,
                    out _,
                    out _))
                continue;

            paired.Add(genos);
            paired.Add(partner);
            pairs.Add(new SeedPair(genos, partner));
        }

        return pairs;
    }

    public int CreateParentEdges(Entity child, IReadOnlyList<Entity> parents, long tick)
    {
        var created = 0;
        foreach (var parent in parents)
        {
            if (!world.IsAlive(parent) || !world.IsAlive(child))
                continue;

            if (service.TryCreatePair(
                    parent,
                    child,
                    RelationshipKind.Parent,
                    RelationshipSource.Seed,
                    tick,
                    out _,
                    out _,
                    out _))
                created++;
        }

        return created;
    }

    private Entity FindPartner(Entity genos, IReadOnlyList<Entity> eligible, HashSet<Entity> paired)
    {
        if (!world.Has<Traits>(genos))
            return Entity.Null;

        var genosAge = world.Get<Alive>(genos).Age;
        var genosTraits = world.Get<Traits>(genos);

        Entity best = Entity.Null;
        var bestGap = int.MaxValue;
        var bestCompatibility = -1;
        ulong bestNetworkId = ulong.MaxValue;

        foreach (var candidate in eligible)
        {
            if (candidate == genos || paired.Contains(candidate))
                continue;

            var ageGap = Math.Abs(genosAge - world.Get<Alive>(candidate).Age);
            if (ageGap > options.MaxAgeGap)
                continue;

            if (!EligibilityPolicy.Evaluate(world, index, genos, candidate, options).Eligible)
                continue;

            var compatibility = world.Has<Traits>(candidate)
                ? AttractionScorer.TraitCompatibilityBasisPoints(genosTraits, world.Get<Traits>(candidate))
                : 0;
            var networkId = NetworkIdOf(candidate);

            if (ageGap > bestGap)
                continue;
            if (ageGap == bestGap && compatibility < bestCompatibility)
                continue;
            if (ageGap == bestGap && compatibility == bestCompatibility && networkId >= bestNetworkId)
                continue;

            best = candidate;
            bestGap = ageGap;
            bestCompatibility = compatibility;
            bestNetworkId = networkId;
        }

        return best;
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
