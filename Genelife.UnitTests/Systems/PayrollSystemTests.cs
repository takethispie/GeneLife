using Arch.Core;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Systems;

namespace Genelife.UnitTests.Systems;

public class PayrollSystemTests
{
    private static readonly SimulationContext Context = new(Tick: 1, DeltaTime: 1f / 20f);

    /// <summary>
    /// Drives a system through the registry so cadence scheduling is exercised
    /// rather than a system-local interval counter.
    /// </summary>
    private static void RunTicks(World world, int payrollInterval, int ticks)
    {
        var registry = new SystemRegistry();
        registry.Register(new PayrollSystem(world, payrollInterval));

        for (var tick = 1; tick <= ticks; tick++)
            registry.Update(new SimulationContext(tick, Context.DeltaTime));
    }

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

        RunTicks(world, payrollInterval: 3, ticks: 3);

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

        RunTicks(world, payrollInterval: 5, ticks: 2);

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

        RunTicks(world, payrollInterval: 2, ticks: 4);

        ref var wallet = ref world.Get<Wallet>(entity);
        Assert.Equal(1000f, wallet.Balance);
    }
}
