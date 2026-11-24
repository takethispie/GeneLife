using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;

namespace Genelife.Systems;

/// <summary>
/// Decays needs for every living sim. The update is per-entity and reads only
/// immutable/read-only components, so it is safe to run across Arch chunks in
/// parallel. Small worlds keep the serial query to avoid scheduling overhead.
/// </summary>
public class NeedsDecaySystem : ISimulationSystem
{
    private const int ParallelThreshold = 512;

    private readonly World world;
    private readonly QueryDescription queryDescription;
    private readonly bool parallel;

    public NeedsDecaySystem(World world, bool parallel = false)
    {
        this.world = world;
        this.parallel = parallel;
        queryDescription = new QueryDescription().WithAll<Needs>().WithNone<Components.Gen.Dead>();
    }

    public string Name => "NeedsDecay";
    public int Order => 100;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.DeltaTime);

    public void Update(float deltaTime)
    {
        if (parallel && world.Size >= ParallelThreshold)
        {
            SimulationParallelism.EnsureScheduler();
            var job = new DecayJob(world, deltaTime);
            world.InlineParallelEntityQuery<DecayJob, Needs>(in queryDescription, ref job);
            return;
        }

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

    private struct DecayJob : IForEachWithEntity<Needs>
    {
        private readonly World world;
        private readonly float deltaTime;

        public DecayJob(World world, float deltaTime)
        {
            this.world = world;
            this.deltaTime = deltaTime;
        }

        public void Update(Entity entity, ref Needs needs) =>
            Decay(world, entity, ref needs, deltaTime);
    }
}
