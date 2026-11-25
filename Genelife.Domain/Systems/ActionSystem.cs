using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Systems;

/// <summary>
/// System that handles action execution and need fulfillment. The pass is
/// per-entity and writes only that entity's action and needs, so it can run
/// across Arch chunks in parallel. Small worlds keep the serial query.
/// </summary>
public class ActionSystem : ISimulationSystem
{
    private const int ParallelThreshold = 512;

    private readonly World world;
    private readonly QueryDescription queryDescription;
    private readonly bool parallel;

    public ActionSystem(World world, bool parallel = false)
    {
        this.world = world;
        this.parallel = parallel;
        queryDescription = new QueryDescription().WithAll<CurrentAction, Needs>().WithNone<Dead>();
    }

    public string Name => "Action";
    public int Order => 300;
    public Cadence Cadence => Cadence.EveryTick;

    void ISimulationSystem.Update(in SimulationContext context) => Update(context.DeltaTime);

    public void Update(float deltaTime)
    {
        if (parallel && world.Size >= ParallelThreshold)
        {
            SimulationParallelism.EnsureScheduler();
            var job = new ActJob(world, deltaTime);
            world.InlineParallelEntityQuery<ActJob, CurrentAction, Needs>(in queryDescription, ref job);
            return;
        }

        world.Query(in queryDescription, (Entity entity, ref CurrentAction action, ref Needs needs) =>
            Act(world, deltaTime, entity, ref action, ref needs));
    }

    private static void Act(
        World world,
        float deltaTime,
        Entity entity,
        ref CurrentAction action,
        ref Needs needs)
    {
        if (action.Type == ActionType.Working)
            return;

        var modifier = world.TryGet<NeedsModifier>(entity, out var stored)
            ? stored
            : NeedsModifier.Neutral;
        var healthFactor = world.TryGet<Health>(entity, out var health)
            ? 0.6f + health.Value / 250f
            : 1f;

        action.TimeRemaining -= deltaTime;

        var canFulfill = CanFulfill(world, entity, action.Type);

        // Apply action effects while performing. Eating and hygiene require a
        // household purchase for genoses that have a ConsumptionState component;
        // legacy/test genoses without one keep the free-restoration behaviour.
        if (canFulfill)
        {
            switch (action.Type)
            {
                case ActionType.Sleeping:
                    needs.Energy = Math.Min(100f, needs.Energy + 5f * modifier.SleepRecovery * healthFactor * deltaTime);
                    break;
                case ActionType.Showering:
                    needs.Hygiene = Math.Min(100f, needs.Hygiene + 10f * modifier.HygieneRecovery * healthFactor * deltaTime);
                    break;
                case ActionType.Eating:
                    needs.Hunger = Math.Min(100f, needs.Hunger + 8f * healthFactor * deltaTime);
                    break;
                case ActionType.UsingToilet:
                    needs.Bladder = Math.Min(100f, needs.Bladder + 15f * deltaTime);
                    break;
                case ActionType.Idle:
                default:
                    break;
            }
        }

        // Action completed
        if (action.TimeRemaining > 0) return;

        // Fulfill the targeted need completely once the action finishes
        if (canFulfill)
        {
            switch (action.Type)
            {
                case ActionType.Sleeping:
                    needs.Energy = 100f;
                    break;
                case ActionType.Showering:
                    needs.Hygiene = 100f;
                    break;
                case ActionType.Eating:
                    needs.Hunger = 100f;
                    break;
                case ActionType.UsingToilet:
                    needs.Bladder = 100f;
                    break;
            }
        }

        action.Type = ActionType.Idle;
        action.TimeRemaining = 0;
    }

    private static bool CanFulfill(World world, Entity entity, ActionType action)
    {
        if (!world.TryGet<ConsumptionState>(entity, out var state))
            return true;

        return action switch
        {
            ActionType.Eating => state.FoodPurchased,
            ActionType.Showering => state.HygienePurchased,
            _ => true
        };
    }

    private struct ActJob : IForEachWithEntity<CurrentAction, Needs>
    {
        private readonly World world;
        private readonly float deltaTime;

        public ActJob(World world, float deltaTime)
        {
            this.world = world;
            this.deltaTime = deltaTime;
        }

        public void Update(Entity entity, ref CurrentAction action, ref Needs needs) =>
            Act(world, deltaTime, entity, ref action, ref needs);
    }
}
