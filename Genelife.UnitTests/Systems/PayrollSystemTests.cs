using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Systems;

namespace Genelife.UnitTests.Systems;

public class PayrollSystemTests
{
    [Fact]
    public void Update_PaysSalaryAfterInterval()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var entity = world.Create(
            new Employee(company, office, "Dev", 1000f),
            new Wallet(0f)
        );
        var system = new PayrollSystem(world, payrollInterval: 3);

        system.Update();
        system.Update();
        system.Update();

        ref var wallet = ref world.Get<Wallet>(entity);
        Assert.Equal(1000f, wallet.Balance);
    }

    [Fact]
    public void Update_DoesNotPayBeforeInterval()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var entity = world.Create(
            new Employee(company, office, "Dev", 1000f),
            new Wallet(0f)
        );
        var system = new PayrollSystem(world, payrollInterval: 5);

        system.Update();
        system.Update();

        ref var wallet = ref world.Get<Wallet>(entity);
        Assert.Equal(0f, wallet.Balance);
    }

    [Fact]
    public void Update_AccumulatesAcrossIntervals()
    {
        var world = World.Create();
        var company = world.Create();
        var office = world.Create();
        var entity = world.Create(
            new Employee(company, office, "Dev", 500f),
            new Wallet(0f)
        );
        var system = new PayrollSystem(world, payrollInterval: 2);

        system.Update();
        system.Update();
        system.Update();
        system.Update();

        ref var wallet = ref world.Get<Wallet>(entity);
        Assert.Equal(1000f, wallet.Balance);
    }
}