using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Systems;

public class DecisionSystemTests
{
    [Fact]
    public void Update_AssignsActionWhenNeedBelowThreshold()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 10f, Energy = 100f, Hygiene = 100f, Bladder = 100f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Eating, action.Type);
        Assert.Equal(5f, action.TimeRemaining);
    }

    [Fact]
    public void Update_DoesNothingWhenNoNeedBelowThreshold()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 80f, Energy = 80f, Hygiene = 80f, Bladder = 80f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Idle, action.Type);
    }

    [Fact]
    public void Update_DoesNotInterruptActiveAction()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 10f, Energy = 100f, Hygiene = 100f, Bladder = 100f },
            new CurrentAction(ActionType.Sleeping, 3f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Sleeping, action.Type);
    }

    [Fact]
    public void Update_PicksLowestNeed()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 50f, Energy = 5f, Hygiene = 60f, Bladder = 70f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Sleeping, action.Type);
        Assert.Equal(8f, action.TimeRemaining);
    }

    [Fact]
    public void Update_Allocates_ZeroBytes_Per_Tick()
    {
        var world = World.Create();
        for (int i = 0; i < 100; i++)
        {
            world.Create(
                new Needs { Hunger = 50f, Energy = 50f, Hygiene = 50f, Bladder = 50f },
                new CurrentAction(ActionType.Idle, 0f)
            );
        }
        var system = new DecisionSystem(world);

        // Warm up: the first Query() pays a one-time cost to build Arch's query
        // cache, so measure the steady state the tick loop actually sees.
        system.Update();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        system.Update();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        // A lambda written inside Update() captures `world` and allocates a fresh
        // closure every tick (measured at 64 B/tick, constant in entity count) --
        // exactly the per-tick allocation the V6 hot path must not have. The
        // callback is hoisted to a field, so steady-state cost must be exactly zero.
        Assert.Equal(0, allocatedAfter - allocatedBefore);
    }

    [Fact]
    public void Update_SkipsPlayerControlledGenoses()
    {
        var world = World.Create();
        world.Create(
            new Needs { Hunger = 5f, Energy = 100f, Hygiene = 100f, Bladder = 100f },
            new CurrentAction(ActionType.Idle, 0f),
            new PlayerControlled("player1")
        );
        var system = new DecisionSystem(world);

        system.Update();

        var query = new QueryDescription().WithAll<PlayerControlled, CurrentAction>();
        world.Query(in query, (ref CurrentAction action) =>
        {
            Assert.Equal(ActionType.Idle, action.Type);
        });
    }

    [Fact]
    public void Update_NpcAtCriticalHunger_AutoEats()
    {
        var world = World.Create();
        var npc = world.Create(
            new Needs { Hunger = 5f, Energy = 100f, Hygiene = 100f, Bladder = 100f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(npc);
        Assert.Equal(ActionType.Eating, action.Type);
    }

    [Fact]
    public void Update_ServesStalestNeedNotLowestValue()
    {
        var world = World.Create();
        // Hunger 25 is nominally the lower value, but hygiene at 28 has been draining
        // far longer: (100-25)/0.5 = 150 s elapsed versus (100-28)/0.2 = 360 s.
        var entity = world.Create(
            new Needs { Hunger = 25f, Energy = 100f, Hygiene = 28f, Bladder = 100f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Showering, action.Type);
        Assert.Equal(3f, action.TimeRemaining);
    }

    [Fact]
    public void Update_StillIgnoresNeedsAboveThreshold()
    {
        var world = World.Create();
        // Hygiene is the stalest, but 90 is not due yet, so nothing should start.
        var entity = world.Create(
            new Needs { Hunger = 90f, Energy = 90f, Hygiene = 90f, Bladder = 90f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new DecisionSystem(world);

        system.Update();

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Idle, action.Type);
    }
}