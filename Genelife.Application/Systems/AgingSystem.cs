using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;

namespace Genelife.Application.Systems;

/// <summary>
/// Advances AgeTicks from the simulation tick. Cadence-safe: the delta comes from
/// LastAgedTick, so skipped passes never lose time. Alive.Age stays the public
/// integer age.
/// </summary>
public class AgingSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public AgingSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? 20;
        query = new QueryDescription().WithAll<Lifecycle, Alive>().WithNone<Dead>();
    }

    public string Name => "Aging";
    public int Order => 90;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick, context.Time);

    public void Update(long tick) => Update(tick, economy.Clock);

    public void Update(long tick, SimulationClock clock)
    {
        var life = economy.Life;
        var updates = new List<(Entity Entity, Lifecycle Lifecycle, Alive Alive, bool AgeChanged, bool MaxReached)>();

        world.Query(in query, (Entity entity, ref Lifecycle lifecycle, ref Alive alive) =>
        {
            var delta = tick - lifecycle.LastAgedTick;
            if (delta <= 0)
                return;

            lifecycle.LastAgedTick = tick;
            var beforeAge = alive.Age;
            lifecycle.AgeTicks = Math.Min(
                (long)life.Policy.MaxAge * clock.TicksPerYear + (clock.TicksPerYear - 1),
                lifecycle.AgeTicks + delta);

            var age = (int)(lifecycle.AgeTicks / Math.Max(1, clock.TicksPerYear));
            age = Math.Min(age, life.Policy.MaxAge);
            if (age == beforeAge)
                return;

            alive.Age = age;
            var maxReached = age >= life.Policy.MaxAge && !lifecycle.MaxAgeFlagged;
            if (maxReached)
                lifecycle.MaxAgeFlagged = true;

            updates.Add((entity, lifecycle, alive, true, maxReached));
        });

        foreach (var update in updates)
        {
            if (!world.IsAlive(update.Entity))
                continue;

            world.Get<Lifecycle>(update.Entity) = update.Lifecycle;
            world.Get<Alive>(update.Entity) = update.Alive;

            var networkId = LifeSystemHelpers.NetworkIdOf(world, update.Entity);
            economy.Emit(new GenosAged(networkId, update.Alive.Age, update.Lifecycle.AgeTicks));

            if (update.MaxReached)
                economy.Emit(new MaxAgeReached(networkId, update.Alive.Age));
        }
    }
}
