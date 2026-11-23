using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Systems;

namespace Genelife.Family;

/// <summary>
/// Quarterly pairing pass. Mutual top choices are processed first, then a
/// deterministic greedy pass. Acceptance and rejection use SimulationRandom so
/// replays produce identical partnerships.
/// </summary>
public sealed class PairingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipService service;
    private readonly RelationshipIndex relationships;
    private readonly RelationshipOptions options;
    private readonly RelationshipCache cache;
    private readonly HouseholdMergeService merger;
    private long rejectionCount;

    public PairingSystem(
        World world,
        EconomyContext economy,
        RelationshipService service,
        RelationshipIndex relationships,
        RelationshipOptions options,
        RelationshipCache cache)
    {
        this.world = world;
        this.economy = economy;
        this.service = service;
        this.relationships = relationships;
        this.options = options;
        this.cache = cache;
        merger = new HouseholdMergeService(world, economy);
    }

    public string Name => "Pairing";
    public int Order => 365;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerPayCycle));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var singles = cache.Scored.Keys
            .Where(sim => IsAvailable(sim, tick))
            .OrderBy(NetworkIdOf)
            .ThenBy(sim => sim.Id)
            .ToList();

        var paired = new HashSet<Entity>();

        foreach (var sim in singles)
        {
            if (paired.Contains(sim) || !IsAvailable(sim, tick))
                continue;

            var top = Top(sim);
            if (top is null || top.Value.Score < options.PairingThreshold)
                continue;

            var reciprocal = Top(top.Value.Sim);
            if (reciprocal is null || reciprocal.Value.Sim != sim ||
                reciprocal.Value.Score < options.PairingThreshold)
                continue;

            if (!AcceptPair(sim, top.Value.Sim, tick))
            {
                RejectPair(sim, top.Value.Sim, "acceptance", tick);
                continue;
            }

            if (TryPair(sim, top.Value.Sim, Math.Min(top.Value.Score, reciprocal.Value.Score), tick))
            {
                paired.Add(sim);
                paired.Add(top.Value.Sim);
            }
        }

        foreach (var sim in singles)
        {
            if (paired.Contains(sim) || !IsAvailable(sim, tick))
                continue;
            if (!cache.Scored.TryGetValue(sim, out var scored))
                continue;

            foreach (var candidate in scored)
            {
                if (candidate.Score < options.PairingThreshold)
                    break;
                if (paired.Contains(candidate.Sim) || !IsAvailable(candidate.Sim, tick))
                    continue;

                var eligibility = EligibilityPolicy.Evaluate(
                    world,
                    relationships,
                    sim,
                    candidate.Sim,
                    options,
                    (first, second) => cache.InCooldown(first, second, tick));
                if (!eligibility.Eligible)
                    continue;

                if (!AcceptPair(sim, candidate.Sim, tick))
                {
                    RejectPair(sim, candidate.Sim, "acceptance", tick);
                    continue;
                }

                if (TryPair(sim, candidate.Sim, candidate.Score, tick))
                {
                    paired.Add(sim);
                    paired.Add(candidate.Sim);
                    break;
                }
            }
        }
    }

    private bool TryPair(Entity first, Entity second, int score, long tick)
    {
        if (!service.TryCreatePair(
                first,
                second,
                RelationshipKind.Partner,
                RelationshipSource.Attraction,
                tick,
                out _,
                out _,
                out _))
            return false;

        cache.Invalidate(first);
        cache.Invalidate(second);

        economy.Emit(new PartnershipFormed(
            NetworkIdOf(first), NetworkIdOf(second), score, tick));

        if (options.CohabitOnPairing && merger.TryMergePartners(
                first, second,
                out _, out _,
                out var fromHouseholdId,
                out var toHouseholdId) &&
            fromHouseholdId != 0 && toHouseholdId != 0 && fromHouseholdId != toHouseholdId)
        {
            economy.Emit(new HouseholdMerged(fromHouseholdId, toHouseholdId, tick));
        }

        return true;
    }

    private void RejectPair(Entity sim, Entity candidate, string reason, long tick)
    {
        var months = options.RejectionCooldownMonths;
        if (months > 0)
        {
            var until = tick + (long)months * economy.Clock.TicksPerMonth;
            cache.SetCooldown(sim, candidate, tick, until);
        }

        // Rejections are high-volume by design. Emit the first 100 so operators
        // can see the reason, then sample one in fifty to bound event-store growth.
        rejectionCount++;
        if (rejectionCount <= 100 || rejectionCount % 50 == 0)
        {
            economy.Emit(new PairingRejected(
                NetworkIdOf(sim), NetworkIdOf(candidate), reason, tick));
        }
    }

    private bool AcceptPair(Entity first, Entity second, long tick) =>
        Roll(first, second, tick) && Roll(second, first, tick);

    private bool Roll(Entity sim, Entity candidate, long tick)
    {
        var stream = $"attraction.pairing.{NetworkIdOf(sim)}.{NetworkIdOf(candidate)}.{tick}";
        return economy.Random.For(stream).NextDouble() < (double)options.PairingAcceptanceChance;
    }

    private bool IsAvailable(Entity sim, long tick) =>
        world.IsAlive(sim) &&
        world.Has<Alive>(sim) &&
        world.Has<Traits>(sim) &&
        !world.Has<Dead>(sim) &&
        !relationships.HasPartner(sim) &&
        !EligibilityPolicy.IsDependent(world, sim);

    private ScoredCandidate? Top(Entity sim) =>
        cache.Scored.TryGetValue(sim, out var scored) && scored.Count > 0 ? scored[0] : null;

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
