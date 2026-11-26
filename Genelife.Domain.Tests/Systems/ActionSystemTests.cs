using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Systems;

public class ActionSystemTests
{
    [Fact]
    public void Update_SleepingRestoresEnergy()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Energy = 50f },
            new CurrentAction(ActionType.Sleeping, 10f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(55f, needs.Energy, 3);
    }

    [Fact]
    public void Update_ShoweringFillsHygieneImmediately()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hygiene = 50f },
            new CurrentAction(ActionType.Showering, 10f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(100f, needs.Hygiene);

        // Filling outright must not shorten the action: it still occupies the genos.
        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Showering, action.Type);
        Assert.Equal(9f, action.TimeRemaining, 3);
    }

    [Fact]
    public void Update_EatingRestoresHunger()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 50f },
            new CurrentAction(ActionType.Eating, 10f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(58f, needs.Hunger, 3);
    }

    [Fact]
    public void Update_UsingToiletRestoresBladder()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Bladder = 50f },
            new CurrentAction(ActionType.UsingToilet, 10f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(65f, needs.Bladder, 3);
    }

    [Fact]
    public void Update_IdleDoesNothing()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 50f, Energy = 50f, Hygiene = 50f, Bladder = 50f },
            new CurrentAction(ActionType.Idle, 0f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(50f, needs.Hunger);
        Assert.Equal(50f, needs.Energy);
        Assert.Equal(50f, needs.Hygiene);
        Assert.Equal(50f, needs.Bladder);
    }

    [Fact]
    public void Update_CompletesActionAndReturnsToIdle()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Energy = 50f },
            new CurrentAction(ActionType.Sleeping, 0.5f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var action = ref world.Get<CurrentAction>(entity);
        Assert.Equal(ActionType.Idle, action.Type);
        Assert.Equal(0f, action.TimeRemaining);
    }

    [Theory]
    [InlineData(ActionType.Sleeping)]
    [InlineData(ActionType.Showering)]
    [InlineData(ActionType.Eating)]
    [InlineData(ActionType.UsingToilet)]
    public void Update_CompletedActionFillsTargetedNeed(ActionType actionType)
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Hunger = 40f, Energy = 40f, Hygiene = 40f, Bladder = 40f },
            new CurrentAction(actionType, 0.5f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(actionType == ActionType.Eating ? 100f : 40f, needs.Hunger);
        Assert.Equal(actionType == ActionType.Sleeping ? 100f : 40f, needs.Energy);
        Assert.Equal(actionType == ActionType.Showering ? 100f : 40f, needs.Hygiene);
        Assert.Equal(actionType == ActionType.UsingToilet ? 100f : 40f, needs.Bladder);
    }

    [Fact]
    public void Update_ClampsNeedAtHundred()
    {
        var world = World.Create();
        var entity = world.Create(
            new Needs { Energy = 99f },
            new CurrentAction(ActionType.Sleeping, 10f)
        );
        var system = new ActionSystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(100f, needs.Energy);
    }
}