using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;
using Genelife.Systems;

namespace Genelife.Family;

/// <summary>
/// Monthly conception pass. Rolls are keyed by canonical pair and calendar
/// month, so adding candidates does not change another pair's outcome.
/// </summary>
public sealed class ConceptionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly RelationshipIndex relationships;
    private readonly PregnancyIndex pregnancies;
    private readonly PregnancyService service;
    private readonly ReproductionOptions options;

    public ConceptionSystem(
        World world,
        EconomyContext economy,
        RelationshipIndex relationships,
        PregnancyIndex pregnancies,
        PregnancyService service,
        ReproductionOptions options)
    {
        this.world = world;
        this.economy = economy;
        this.relationships = relationships;
        this.pregnancies = pregnancies;
        this.service = service;
        this.options = options;
    }

    public string Name => "Conception";
    public int Order => 370;
    public Cadence Cadence => new(Math.Max(1, economy.Clock.TicksPerMonth));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        options.Validate();

        var sims = new List<Entity>();
        world.Query(in new QueryDescription().WithAll<Alive, Traits, NetworkId>().WithNone<Dead>(),
            (Entity entity, ref Alive _, ref Traits _, ref NetworkId _) => sims.Add(entity));
        sims.Sort((a, b) =>
        {
            var byNetworkId = NetworkIdOf(a).CompareTo(NetworkIdOf(b));
            return byNetworkId != 0 ? byNetworkId : a.Id.CompareTo(b.Id);
        });

        var seenPairs = new HashSet<(ulong First, ulong Second)>();
        foreach (var sim in sims)
        {
            foreach (var partner in relationships.PartnersOf(sim))
            {
                if (!world.IsAlive(partner) || world.Has<Dead>(partner))
                    continue;

                var firstId = NetworkIdOf(sim);
                var secondId = NetworkIdOf(partner);
                var key = firstId <= secondId ? (firstId, secondId) : (secondId, firstId);
                if (!seenPairs.Add(key))
                    continue;

                var carrying = ChooseCarryingParent(sim, partner);
                if (carrying == Entity.Null)
                    continue;
                var other = carrying == sim ? partner : sim;

                if (!Eligible(carrying, other, tick))
                    continue;

                var stream = $"reproduction.conception.{key.Item1}.{key.Item2}." +
                             $"{economy.Clock.YearOf(tick)}.{economy.Clock.MonthOfYear(tick)}";
                if (economy.Random.For(stream).NextDouble() >= (double)options.MonthlyConceptionChance)
                    continue;

                var health = world.Has<Health>(carrying)
                    ? (int)Math.Round(world.Get<Health>(carrying).Value)
                    : 100;
                var due = tick + (long)options.PregnancyMonths * economy.Clock.TicksPerMonth;
                if (!service.TryStart(
                        carrying,
                        other,
                        tick,
                        due,
                        health,
                        PregnancySource.Conception,
                        options,
                        out var pregnancy,
                        out _))
                    continue;

                var pregnancyId = NetworkIdOf(pregnancy);
                economy.Emit(new PregnancyStarted(
                    pregnancyId,
                    NetworkIdOf(carrying),
                    NetworkIdOf(other),
                    tick,
                    due,
                    tick));
                economy.Emit(new ConceptionOccurred(
                    NetworkIdOf(carrying),
                    NetworkIdOf(other),
                    pregnancyId,
                    tick));
            }
        }
    }

    private Entity ChooseCarryingParent(Entity first, Entity second)
    {
        var firstTraits = world.Get<Traits>(first);
        var secondTraits = world.Get<Traits>(second);
        if (firstTraits.Sex != secondTraits.Sex)
            return firstTraits.Sex == Sex.Female ? first : second;
        if (!options.AllowSameSexCarryingPair)
            return Entity.Null;
        return NetworkIdOf(first) <= NetworkIdOf(second) ? first : second;
    }

    private bool Eligible(Entity carrying, Entity other, long tick)
    {
        if (world.Has<Dead>(carrying) || world.Has<Dead>(other))
            return false;
        if (PregnancyService.IsDependent(world, carrying) || PregnancyService.IsDependent(world, other))
            return false;
        if (pregnancies.TryGetActive(carrying, out _))
            return false;
        if (pregnancies.IsInLossCooldown(carrying, tick, options, economy.Clock))
            return false;

        var age = world.Has<Alive>(carrying) ? world.Get<Alive>(carrying).Age : 0;
        if (age < options.MinimumConceptionAge || age > options.MaximumConceptionAge)
            return false;
        var otherAge = world.Has<Alive>(other) ? world.Get<Alive>(other).Age : 0;
        if (otherAge < options.MinimumConceptionAge)
            return false;

        var health = world.Has<Health>(carrying) ? world.Get<Health>(carrying).Value : 100f;
        return health >= options.MinimumConceptionHealth;
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
