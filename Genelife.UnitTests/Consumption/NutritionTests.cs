using Arch.Core;
using Genelife.Components.Consumption;
using Genelife.Components.Gen;
using Genelife.Components.Life;
using Genelife.Economy;
using Genelife.Systems;

namespace Genelife.UnitTests.Consumption;

public class NutritionTests
{
    [Fact]
    public void PoorNutritionLowersHealthAndBalancedNutritionHelps()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var poor = world.Create(new Health(50f), new NutritionState { Balance = -30 });
        var good = world.Create(new Health(50f), new NutritionState { Balance = 30 });
        var neutral = world.Create(new Health(50f), new NutritionState { Balance = 0 });
        var system = new NutritionSystem(world, economy, interval: 1);

        system.Update(100);

        Assert.True(world.Get<Health>(poor).Value < 50f);
        Assert.True(world.Get<Health>(good).Value > 50f);
        Assert.Equal(50f, world.Get<Health>(neutral).Value);
        Assert.Equal(1, world.Get<NutritionState>(poor).ConsecutivePoorDays);
        Assert.Equal(0, world.Get<NutritionState>(good).ConsecutivePoorDays);
    }

    [Fact]
    public void NutritionIsAModifierNotASecondHealthModel()
    {
        var world = World.Create();
        var economy = new EconomyContext(world);
        var sim = world.Create(new Health(80f), new NutritionState { Balance = -100 });
        var system = new NutritionSystem(world, economy, interval: 1);
        for (var i = 0; i < 10; i++)
            system.Update(100 + i);
        Assert.True(world.Get<Health>(sim).Value >= 1f);
        Assert.True(world.Has<Health>(sim));
    }
}
