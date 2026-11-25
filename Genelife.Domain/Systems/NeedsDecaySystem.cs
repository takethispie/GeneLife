using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;

namespace Genelife.Systems;

/// <summary>
/// Decays needs for every living genos.
/// </summary>
public class NeedsDecaySystem : ISimulationSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;

    public NeedsDecaySystem(World world)
    {
        this.world = world;
        queryDescription = new QueryDescription().WithAll<Needs>().WithNone<Components.Gen.Dead>();
    }

    public string Name => "NeedsDecay";
    public int Order => 100;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.DeltaTime);

    public void Update(float deltaTime)
    {
        world.Query(in queryDescription, (Entity entity, ref Needs needs) =>
            Decay(world, entity, ref needs, deltaTime));
    }

    private static void Decay(World world, Entity entity, ref Needs needs, float deltaTime)
    {
        var modifier = world.TryGet<NeedsModifier>(entity, out var stored)
            ? stored
            : NeedsModifier.Neutral;

        // Decay rates per second, scaled by the housing profile.
        needs.Hunger -= 0.5f * modifier.HungerDecay * deltaTime;
        needs.Energy -= 0.3f * modifier.EnergyDecay * deltaTime;
        needs.Hygiene -= 0.2f * modifier.HygieneDecay * deltaTime;
        needs.Bladder -= 0.4f * modifier.BladderDecay * deltaTime;

        needs.Hunger = Math.Max(0f, needs.Hunger);
        needs.Energy = Math.Max(0f, needs.Energy);
        needs.Hygiene = Math.Max(0f, needs.Hygiene);
        needs.Bladder = Math.Max(0f, needs.Bladder);
    }
}
