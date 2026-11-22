using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Systems;

public class DecisionSystem : ISimulationSystem
{
    private readonly World world;
    private readonly QueryDescription queryDescription;
    private const float CriticalThreshold = 30f;

    public DecisionSystem(World world)
    {
        this.world = world;
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
        world.Query(in queryDescription, (Entity entity, ref CurrentAction action, ref Needs needs) =>
        {
            if (action.Type != ActionType.Idle)
                return;

            // A work shift owns the working action; WorkScheduleSystem decides
            // whether a critical need interrupts the shift.
            if (world.Has<WorkSchedule>(entity) && world.Get<WorkSchedule>(entity).IsAtWork)
                return;

            var threshold = world.Has<Lifecycle>(entity) &&
                            world.Get<Lifecycle>(entity).Stage == LifeStage.Work
                ? 15f
                : CriticalThreshold;

            var lowestNeed = GetLowestNeed(needs);

            if (lowestNeed.Value < threshold)
            {
                action.Type = lowestNeed.ActionType;
                action.TimeRemaining = lowestNeed.Duration;
            }
        });
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
}
