using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Systems;

namespace Genelife.Family;

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

    public AttractionSystem(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        RelationshipOptions options,
        RelationshipCache cache)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.options = options;
        this.cache = cache;
    }

    public string Name => "Attraction";
    public int Order => 360;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerPayCycle));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var sims = cache.Candidates.Keys
            .Where(sim => world.IsAlive(sim) && world.Has<Alive>(sim) && world.Has<Traits>(sim) &&
                          !world.Has<Dead>(sim) && !relationships.HasPartner(sim))
            .OrderBy(NetworkIdOf)
            .ThenBy(sim => sim.Id)
            .ToList();

        foreach (var sim in sims)
        {
            if (!cache.Candidates.TryGetValue(sim, out var candidates))
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
                    sim,
                    candidate,
                    options,
                    (first, second) => cache.InCooldown(first, second, tick));
                if (!eligibility.Eligible)
                    continue;

                var input = AttractionInputFactory.Create(
                    world,
                    economy,
                    sim,
                    candidate,
                    !relationships.HasPartner(sim) && !cache.InCooldown(sim, candidate, tick),
                    !relationships.HasPartner(candidate) && !cache.InCooldown(sim, candidate, tick));
                var result = AttractionScorer.Score(input, eligibility, options);
                if (result.Score > 0)
                    scored.Add(new ScoredCandidate(candidate, result.Score));
            }

            scored.Sort((a, b) =>
            {
                var byScore = b.Score.CompareTo(a.Score);
                if (byScore != 0)
                    return byScore;
                var byNetworkId = NetworkIdOf(a.Sim).CompareTo(NetworkIdOf(b.Sim));
                return byNetworkId != 0 ? byNetworkId : a.Sim.Id.CompareTo(b.Sim.Id);
            });

            if (scored.Count > options.MaxCandidatesPerSim)
                scored.RemoveRange(options.MaxCandidatesPerSim, scored.Count - options.MaxCandidatesPerSim);

            if (scored.Count > 0)
                cache.Scored[sim] = scored;
            else
                cache.Scored.Remove(sim);
        }

        foreach (var stale in cache.Scored.Keys.Where(sim => !sims.Contains(sim)).ToList())
            cache.Scored.Remove(stale);
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
