using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// Picks a critical-need action for idle sims. Per-entity only: it writes
/// <see cref="CurrentAction"/> and reads <see cref="Needs"/>, work schedule and
/// lifecycle, so it can run across Arch chunks in parallel. Small worlds keep
/// the serial query.
/// </summary>
public class DecisionSystem : ISimulationSystem
{
    private const float CriticalThreshold = 30f;
    private const int ParallelThreshold = 512;

    private readonly World world;
    private readonly QueryDescription queryDescription;
    private readonly bool parallel;

    public DecisionSystem(World world, bool parallel = false)
    {
        this.world = world;
        this.parallel = parallel;
        queryDescription = new QueryDescription()
            .WithAll<CurrentAction, Needs>()
            .WithNone<PlayerControlled, Dead>();
    }

    public string Name => "Decision";
    public int Order => 200;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update();

    public void Update()
    {
        if (parallel && world.Size >= ParallelThreshold)
        {
            SimulationParallelism.EnsureScheduler();
            var job = new DecideJob(world);
            world.InlineParallelEntityQuery<DecideJob, CurrentAction, Needs>(in queryDescription, ref job);
            return;
        }

        world.Query(in queryDescription, (Entity entity, ref CurrentAction action, ref Needs needs) =>
            Decide(world, entity, ref action, ref needs));
    }

    private static void Decide(World world, Entity entity, ref CurrentAction action, ref Needs needs)
    {
        if (action.Type != ActionType.Idle)
            return;

        // A work shift owns the working action; WorkScheduleSystem decides
        // whether a critical need interrupts the shift.
        if (world.TryGet<WorkSchedule>(entity, out var schedule) && schedule.IsAtWork)
            return;

        var threshold = world.TryGet<Lifecycle>(entity, out var lifecycle) &&
                        lifecycle.Stage == LifeStage.Work
            ? 15f
            : CriticalThreshold;

        var lowestNeed = GetLowestNeed(needs);

        if (lowestNeed.Value < threshold)
        {
            action.Type = lowestNeed.ActionType;
            action.TimeRemaining = lowestNeed.Duration;
        }
    }

    private static NeedInfo GetLowestNeed(Needs needs)
    {
        var lowestValue = needs.Hunger;
        var lowestAction = ActionType.Eating;
        var lowestDuration = 5f;

        if (needs.Energy < lowestValue)
        {
            lowestValue = needs.Energy;
            lowestAction = ActionType.Sleeping;
            lowestDuration = 8f;
        }

        if (needs.Hygiene < lowestValue)
        {
            lowestValue = needs.Hygiene;
            lowestAction = ActionType.Showering;
            lowestDuration = 3f;
        }

        if (needs.Bladder < lowestValue)
        {
            lowestValue = needs.Bladder;
            lowestAction = ActionType.UsingToilet;
            lowestDuration = 2f;
        }

        return new NeedInfo(lowestValue, lowestAction, lowestDuration);
    }

    private readonly record struct NeedInfo(float Value, ActionType ActionType, float Duration);

    private struct DecideJob : IForEachWithEntity<CurrentAction, Needs>
    {
        private readonly World world;

        public DecideJob(World world)
        {
            this.world = world;
        }

        public void Update(Entity entity, ref CurrentAction action, ref Needs needs) =>
            Decide(world, entity, ref action, ref needs);
    }
}
