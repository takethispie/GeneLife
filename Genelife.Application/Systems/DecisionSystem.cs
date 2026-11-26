using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Enums;

namespace Genelife.Application.Systems;

/// <summary>
/// Picks a need-fulfilling action for idle genoses.
/// </summary>
public class DecisionSystem : ISimulationSystem
{
    private const float CriticalThreshold = 50f;

    // Base decay per second, mirroring NeedsDecaySystem. Dividing a need's shortfall
    // by its own decay rate turns it into "seconds since this need was last full",
    // which is comparable across needs that drain at different speeds.
    private const float HungerDecayPerSecond = 0.5f;
    private const float EnergyDecayPerSecond = 0.3f;
    private const float HygieneDecayPerSecond = 0.2f;
    private const float BladderDecayPerSecond = 0.4f;

    private readonly World world;
    private readonly QueryDescription queryDescription;

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

        var modifier = world.TryGet<NeedsModifier>(entity, out var stored)
            ? stored
            : NeedsModifier.Neutral;

        var need = SelectNeed(in needs, in modifier, CriticalThreshold);
        if (need.ActionType == ActionType.Idle)
            return;

        action.Type = need.ActionType;
        action.TimeRemaining = need.Duration;
    }

    /// <summary>
    /// Among the needs below <paramref name="threshold"/>, picks the one that has been
    /// draining for the longest time. Selecting on absolute value alone starves the
    /// slowest-decaying need: hunger (0.5/s) always undercuts hygiene (0.2/s), so
    /// hygiene only ever got served last and sat far below the others on average.
    /// Ranking by staleness instead gives every need the same service interval.
    /// </summary>
    private static NeedInfo SelectNeed(in Needs needs, in NeedsModifier modifier, float threshold)
    {
        var selected = ActionType.Idle;
        var duration = 0f;
        var longest = 0f;

        if (needs.Hunger < threshold)
        {
            longest = Staleness(needs.Hunger, HungerDecayPerSecond * modifier.HungerDecay);
            selected = ActionType.Eating;
            duration = 5f;
        }

        if (needs.Energy < threshold)
        {
            var staleness = Staleness(needs.Energy, EnergyDecayPerSecond * modifier.EnergyDecay);
            if (selected == ActionType.Idle || staleness > longest)
            {
                longest = staleness;
                selected = ActionType.Sleeping;
                duration = 8f;
            }
        }

        if (needs.Hygiene < threshold)
        {
            var staleness = Staleness(needs.Hygiene, HygieneDecayPerSecond * modifier.HygieneDecay);
            if (selected == ActionType.Idle || staleness > longest)
            {
                longest = staleness;
                selected = ActionType.Showering;
                duration = 3f;
            }
        }

        if (needs.Bladder < threshold)
        {
            var staleness = Staleness(needs.Bladder, BladderDecayPerSecond * modifier.BladderDecay);
            if (selected == ActionType.Idle || staleness > longest)
            {
                selected = ActionType.UsingToilet;
                duration = 2f;
            }
        }

        return new NeedInfo(selected, duration);
    }

    private static float Staleness(float value, float rate)
        => rate <= 0f ? 0f : MathF.Max(0f, 100f - value) / rate;

    private readonly record struct NeedInfo(ActionType ActionType, float Duration);
}