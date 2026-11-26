using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;

namespace Genelife.Application.Systems;

/// <summary>
/// Gradual decline for the 80+ stage. Slows recovery and adds a small monthly
/// health loss on top of the age drift in <see cref="HealthSystem"/>.
/// </summary>
public class ElderDeclineSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly QueryDescription query;
    private readonly int interval;

    public ElderDeclineSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
        query = new QueryDescription().WithAll<Alive, Lifecycle, Health>().WithNone<Dead>();
    }

    public string Name => "ElderDecline";
    public int Order => 560;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var events = new List<object>();
        var policy = economy.Life.Policy;

        world.Query(in query, (Entity entity, ref Alive alive, ref Lifecycle lifecycle, ref Health health) =>
        {
            if (lifecycle.Stage != Enums.LifeStage.Elderly && alive.Age < policy.ElderlyAge)
                return;

            var f = Math.Clamp((alive.Age - policy.ElderlyAge) / 25f, 0f, 1f);
            if (world.Has<NeedsModifier>(entity))
            {
                var modifier = world.Get<NeedsModifier>(entity);
                modifier.SleepRecovery *= 1f - 0.4f * f;
                modifier.HygieneRecovery *= 1f - 0.3f * f;
                modifier.HealthDrift -= 0.25f * f;
                world.Get<NeedsModifier>(entity) = modifier;
            }

            var before = health.Value;
            health.Value = Math.Clamp(health.Value - 0.25f * f, 0f, 100f);
            health.DeclineRate -= 0.25f * f;
            world.Get<Health>(entity) = health;

            if (before >= 30f && health.Value < 30f)
                events.Add(new CareNeeded(LifeSystemHelpers.NetworkIdOf(world, entity), health.Value, tick));
        });

        foreach (var @event in events)
            economy.Emit(@event);
    }
}
