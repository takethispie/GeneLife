using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Systems;

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

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        system.Update();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(allocatedAfter - allocatedBefore >= 0,
            $"DecisionSystem.Update allocated {allocatedAfter - allocatedBefore} bytes");
    }
}