using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Application.Systems;

using Genelife.Family;
namespace Genelife.Application.Systems;

/// <summary>
/// Quarterly scoring pass. It reads the monthly candidate cache and stores a
/// sorted, explainable score list for the pairing pass.
/// </summary>
public sealed class AttractionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipOptions options;
    private readonly RelationshipCache cache;

    private readonly int cadenceOffset;

    public AttractionSystem(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipOptions options,
        RelationshipCache cache,
        int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.options = options;
        this.cache = cache;
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "Attraction";
    public int Order => 360;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerPayCycle), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var genoses = cache.Candidates.Keys
            .Where(genos => world.IsAlive(genos) && world.Has<Alive>(genos) && world.Has<Traits>(genos) &&
                          !world.Has<Dead>(genos) && !relationships.HasPartner(genos))
            .OrderBy(NetworkIdOf)
            .ThenBy(genos => genos.Id)
            .ToList();

        foreach (var genos in genoses)
        {
            if (!cache.Candidates.TryGetValue(genos, out var candidates))
                continue;

            var scored = new List<ScoredCandidate>(candidates.Count);
            foreach (var candidate in candidates)
            {
                if (!world.IsAlive(candidate) || !world.Has<Alive>(candidate) ||
                    !world.Has<Traits>(candidate) || world.Has<Dead>(candidate) ||
                    relationships.HasPartner(candidate))
                    continue;

                var eligibility = EligibilityPolicy.Evaluate(
                    world,
                    relationships,
                    genos,
                    candidate,
                    options,
                    (first, second) => cache.InCooldown(first, second, tick));
                if (!eligibility.Eligible)
                    continue;

                var input = AttractionInputFactory.Create(
                    world,
                    economy,
                    genos,
                    candidate,
                    !relationships.HasPartner(genos) && !cache.InCooldown(genos, candidate, tick),
                    !relationships.HasPartner(candidate) && !cache.InCooldown(genos, candidate, tick));
                var result = AttractionScorer.Score(input, eligibility, options);
                if (result.Score > 0)
                    scored.Add(new ScoredCandidate(candidate, result.Score));
            }

            scored.Sort((a, b) =>
            {
                var byScore = b.Score.CompareTo(a.Score);
                if (byScore != 0)
                    return byScore;
                var byNetworkId = NetworkIdOf(a.Genos).CompareTo(NetworkIdOf(b.Genos));
                return byNetworkId != 0 ? byNetworkId : a.Genos.Id.CompareTo(b.Genos.Id);
            });

            if (scored.Count > options.MaxCandidatesPerGenos)
                scored.RemoveRange(options.MaxCandidatesPerGenos, scored.Count - options.MaxCandidatesPerGenos);

            if (scored.Count > 0)
                cache.Scored[genos] = scored;
            else
                cache.Scored.Remove(genos);
        }

        foreach (var stale in cache.Scored.Keys.Where(genos => !genoses.Contains(genos)).ToList())
            cache.Scored.Remove(stale);
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
