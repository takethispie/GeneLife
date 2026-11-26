using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Application.Systems;

namespace Genelife.UnitTests.Systems;

public class NeedsDecaySystemTests
{
    [Fact]
    public void Update_DecaysAllNeeds()
    {
        var world = World.Create();
        var entity = world.Create(new Needs());
        var system = new NeedsDecaySystem(world);

        system.Update(1f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(99.5f, needs.Hunger, 3);
        Assert.Equal(99.7f, needs.Energy, 3);
        Assert.Equal(99.8f, needs.Hygiene, 3);
        Assert.Equal(99.6f, needs.Bladder, 3);
    }

    [Fact]
    public void Update_ClampsAtZero()
    {
        var world = World.Create();
        var entity = world.Create(new Needs { Hunger = 0.1f, Energy = 0.1f, Hygiene = 0.1f, Bladder = 0.1f });
        var system = new NeedsDecaySystem(world);

        system.Update(10f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(0f, needs.Hunger);
        Assert.Equal(0f, needs.Energy);
        Assert.Equal(0f, needs.Hygiene);
        Assert.Equal(0f, needs.Bladder);
    }

    [Fact]
    public void Update_ScalesWithDeltaTime()
    {
        var world = World.Create();
        var entity = world.Create(new Needs());
        var system = new NeedsDecaySystem(world);

        system.Update(2f);

        ref var needs = ref world.Get<Needs>(entity);
        Assert.Equal(99f, needs.Hunger, 3);
    }
}