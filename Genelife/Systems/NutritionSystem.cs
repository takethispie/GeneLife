using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Messages.Events;

namespace Genelife.Systems;

/// <summary>
/// Monthly nutrition modifier over the existing health model. It never adds a
/// second health stat; poor nutrition lowers health, balanced nutrition slows
/// decline. Sim-level nutrition is created by the player consume path.
/// </summary>
public sealed class NutritionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly EconomyContext economy;
    private readonly int interval;

    public NutritionSystem(World world, EconomyContext economy, int? interval = null)
    {
        this.world = world;
        this.economy = economy;
        this.interval = interval ?? economy.Clock.TicksPerMonth;
    }

    public string Name => "Nutrition";
    public int Order => 478;
    public Cadence Cadence => new(Math.Max(1, interval));

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.Tick);

    public void Update(long tick)
    {
        var query = new QueryDescription().WithAll<NutritionState>().WithNone<Dead>();
        world.Query(in query, (Entity entity, ref NutritionState nutrition) =>
        {
            var delta = nutrition.Balance switch
            {
                <= -20 => -0.20f,
                < 0 => -0.05f,
                > 20 => 0.05f,
                _ => 0f
            };

            nutrition.ConsecutivePoorDays = nutrition.Balance < 0
                ? nutrition.ConsecutivePoorDays + 1
                : 0;

            if (delta != 0f && world.Has<Health>(entity))
            {
                var health = world.Get<Health>(entity);
                health.Value = Math.Clamp(health.Value + delta, 1f, 100f);
                world.Get<Health>(entity) = health;
            }

            world.Get<NutritionState>(entity) = nutrition;
            economy.Emit(new NutritionHealthChanged(
                NetworkIdOf(entity),
                nutrition.Balance,
                nutrition.ConsecutivePoorDays,
                delta,
                tick));
        });
    }

    private ulong NetworkIdOf(Entity entity) =>
        world.Has<NetworkId>(entity) ? world.Get<NetworkId>(entity).Value : (ulong)entity.Id;
}
