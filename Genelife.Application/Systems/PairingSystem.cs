using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Application.Systems;

using Genelife.Family;
namespace Genelife.Application.Systems;

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

    private readonly int cadenceOffset;

    public PairingSystem(
        World world,
        EconomyContext economy,
        RelationshipService service,
        RelationshipIndex relationships,
        RelationshipOptions options,
        RelationshipCache cache,
        int cadenceOffset = 0)
    {
        this.world = world;
        this.economy = economy;
        this.service = service;
        this.relationships = relationships;
        this.options = options;
        this.cache = cache;
        merger = new HouseholdMergeService(world, economy);
        this.cadenceOffset = cadenceOffset;
    }

    public string Name => "Pairing";
    public int Order => 365;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerPayCycle), cadenceOffset);

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var singles = cache.Scored.Keys
            .Where(genos => IsAvailable(genos, tick))
            .OrderBy(NetworkIdOf)
            .ThenBy(genos => genos.Id)
            .ToList();

        var paired = new HashSet<Entity>();

        foreach (var genos in singles)
        {
            if (paired.Contains(genos) || !IsAvailable(genos, tick))
                continue;

            var top = Top(genos);
            if (top is null || top.Value.Score < options.PairingThreshold)
                continue;

            var reciprocal = Top(top.Value.Genos);
            if (reciprocal is null || reciprocal.Value.Genos != genos ||
                reciprocal.Value.Score < options.PairingThreshold)
                continue;

            if (!AcceptPair(genos, top.Value.Genos, tick))
            {
                RejectPair(genos, top.Value.Genos, "acceptance", tick);
                continue;
            }

            if (TryPair(genos, top.Value.Genos, Math.Min(top.Value.Score, reciprocal.Value.Score), tick))
            {
                paired.Add(genos);
                paired.Add(top.Value.Genos);
            }
        }

        foreach (var genos in singles)
        {
            if (paired.Contains(genos) || !IsAvailable(genos, tick))
                continue;
            if (!cache.Scored.TryGetValue(genos, out var scored))
                continue;

            foreach (var candidate in scored)
            {
                if (candidate.Score < options.PairingThreshold)
                    break;
                if (paired.Contains(candidate.Genos) || !IsAvailable(candidate.Genos, tick))
                    continue;

                var eligibility = EligibilityPolicy.Evaluate(
                    world,
                    relationships,
                    genos,
                    candidate.Genos,
                    options,
                    (first, second) => cache.InCooldown(first, second, tick));
                if (!eligibility.Eligible)
                    continue;

                if (!AcceptPair(genos, candidate.Genos, tick))
                {
                    RejectPair(genos, candidate.Genos, "acceptance", tick);
                    continue;
                }

                if (TryPair(genos, candidate.Genos, candidate.Score, tick))
                {
                    paired.Add(genos);
                    paired.Add(candidate.Genos);
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

    private void RejectPair(Entity genos, Entity candidate, string reason, long tick)
    {
        var months = options.RejectionCooldownMonths;
        if (months > 0)
        {
            var until = tick + (long)months * economy.Clock.TicksPerMonth;
            cache.SetCooldown(genos, candidate, tick, until);
        }

        // Rejections are high-volume by design. Emit the first 100 so operators
        // can see the reason, then sample one in fifty to bound event-store growth.
        rejectionCount++;
        if (rejectionCount <= 100 || rejectionCount % 50 == 0)
        {
            economy.Emit(new PairingRejected(
                NetworkIdOf(genos), NetworkIdOf(candidate), reason, tick));
        }
    }

    private bool AcceptPair(Entity first, Entity second, long tick) =>
        Roll(first, second, tick) && Roll(second, first, tick);

    private bool Roll(Entity genos, Entity candidate, long tick)
    {
        var stream = $"attraction.pairing.{NetworkIdOf(genos)}.{NetworkIdOf(candidate)}.{tick}";
        return economy.Random.For(stream).NextDouble() < (double)options.PairingAcceptanceChance;
    }

    private bool IsAvailable(Entity genos, long tick) =>
        world.IsAlive(genos) &&
        world.Has<Alive>(genos) &&
        world.Has<Traits>(genos) &&
        !world.Has<Dead>(genos) &&
        !relationships.HasPartner(genos) &&
        !EligibilityPolicy.IsDependent(world, genos);

    private ScoredCandidate? Top(Entity genos) =>
        cache.Scored.TryGetValue(genos, out var scored) && scored.Count > 0 ? scored[0] : null;

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
