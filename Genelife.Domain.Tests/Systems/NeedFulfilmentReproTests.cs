using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Enums;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Systems;

/// <summary>
/// Differential repro for "needs never get set back to 100".
/// Same needs, same systems — the only variables are (a) whether the genos has a
/// work shift that can clobber the CurrentAction slot and (b) the purchase gate.
/// </summary>
public class NeedFulfilmentReproTests
{
    private const float DeltaTime = 1f / 20f;
    private const int MaxTicks = 6000;

    [Fact]
    public void Control_NoShift_ReachesFullHungerAndEnergy()
    {
        var r = Run(employed: false, hasConsumptionState: false, foodPurchased: false);
        Assert.True(r.ReachedHunger, $"control hunger: {r}");
        Assert.True(r.ReachedEnergy, $"control energy: {r}");
    }

    [Fact]
    public void Gate_NoFoodPurchase_BlocksHungerAtFull()
    {
        var r = Run(employed: false, hasConsumptionState: true, foodPurchased: false);
        Assert.False(r.ReachedHunger, $"hunger should be gated when food was not bought: {r}");
    }

    [Fact]
    public void Employed_ReachesFullHungerAndEnergy()
    {
        var r = Run(employed: true, hasConsumptionState: true, foodPurchased: true);
        Assert.True(r.ReachedEnergy, $"employed genos never reached energy 100: {r}");
        Assert.True(r.ReachedHunger, $"employed genos never reached hunger 100: {r}");
    }

    private static RunResult Run(bool employed, bool hasConsumptionState, bool foodPurchased)
    {
        using var world = World.Create();
        var economy = new EconomyContext(world);

        var genos = employed
            ? world.Create(
                new NetworkId(1),
                new Needs { Hunger = 10f, Energy = 10f, Hygiene = 90f, Bladder = 90f },
                new CurrentAction(ActionType.Idle, 0f),
                new Lifecycle(LifeStage.Work, 0, 0),
                new Employee(Entity.Null, Entity.Null, "worker", 10m),
                new WorkSchedule(Entity.Null))
            : world.Create(
                new NetworkId(1),
                new Needs { Hunger = 10f, Energy = 10f, Hygiene = 90f, Bladder = 90f },
                new CurrentAction(ActionType.Idle, 0f));

        if (hasConsumptionState)
        {
            world.Add(genos, new ConsumptionState
            {
                FoodPurchased = foodPurchased,
                HygienePurchased = foodPurchased
            });
        }

        var decay = new NeedsDecaySystem(world);
        var decision = new DecisionSystem(world);
        var action = new ActionSystem(world);
        var work = employed ? new WorkScheduleSystem(world, economy, interval: 1) : null;

        var maxHunger = 0f;
        var maxEnergy = 0f;
        var reachedHunger = false;
        var reachedEnergy = false;
        var eatingCompletions = 0;
        var eatingClobbered = 0;
        var sleepClobbered = 0;

        for (var tick = 1; tick <= MaxTicks; tick++)
        {
            var before = world.Get<CurrentAction>(genos);

            var context = new SimulationContext(tick, DeltaTime);
            ((ISimulationSystem)decay).Update(in context);
            ((ISimulationSystem)decision).Update(in context);
            ((ISimulationSystem)action).Update(in context);
            if (work is not null)
                ((ISimulationSystem)work).Update(in context);

            var after = world.Get<CurrentAction>(genos);

            if (before.Type == ActionType.Eating)
            {
                if (after.Type == ActionType.Idle && after.TimeRemaining == 0f)
                    eatingCompletions++;
                else if (after.Type != ActionType.Eating && after.Type != ActionType.Idle)
                    eatingClobbered++;
            }
            if (before.Type == ActionType.Sleeping && after.Type != ActionType.Sleeping && after.Type != ActionType.Idle)
                sleepClobbered++;

            var current = world.Get<Needs>(genos);
            maxHunger = MathF.Max(maxHunger, current.Hunger);
            maxEnergy = MathF.Max(maxEnergy, current.Energy);
            if (current.Hunger >= 100f) reachedHunger = true;
            if (current.Energy >= 100f) reachedEnergy = true;
        }

        var final = world.Get<Needs>(genos);
        return new RunResult(
            employed, hasConsumptionState, foodPurchased,
            reachedHunger, reachedEnergy,
            maxHunger, maxEnergy, final.Hunger, final.Energy,
            eatingCompletions, eatingClobbered, sleepClobbered);
    }

    private readonly record struct RunResult(
        bool Employed, bool HasGate, bool FoodPurchased,
        bool ReachedHunger, bool ReachedEnergy,
        float MaxHunger, float MaxEnergy, float FinalHunger, float FinalEnergy,
        int EatingCompletions, int EatingClobbered, int SleepClobbered)
    {
        public override string ToString() =>
            $"employed={Employed} gate={HasGate} food={FoodPurchased} " +
            $"maxHunger={MaxHunger:F1} maxEnergy={MaxEnergy:F1} " +
            $"finalHunger={FinalHunger:F1} finalEnergy={FinalEnergy:F1} " +
            $"eatCompletions={EatingCompletions} eatClobbered={EatingClobbered} sleepClobbered={SleepClobbered}";
    }
}