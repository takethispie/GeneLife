using Genelife.Application.Systems;

namespace Genelife.UnitTests.Systems;

public class SystemRegistryTests
{
    [Fact]
    public void Register_SortsSystemsByExplicitOrder()
    {
        var registry = new SystemRegistry();
        registry.Register(new FakeSystem("Third", order: 300));
        registry.Register(new FakeSystem("First", order: 100));
        registry.Register(new FakeSystem("Second", order: 200));

        Assert.Equal(new[] { "First", "Second", "Third" }, registry.Systems.Select(system => system.Name));
    }

    [Fact]
    public void Register_RejectsDuplicateOrder()
    {
        var registry = new SystemRegistry();
        registry.Register(new FakeSystem("First", order: 100));

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.Register(new FakeSystem("Other", order: 100)));
        Assert.Contains("order 100", ex.Message);
    }

    [Fact]
    public void Register_RejectsDuplicateName()
    {
        var registry = new SystemRegistry();
        registry.Register(new FakeSystem("Same", order: 100));

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(new FakeSystem("Same", order: 200)));
    }

    [Fact]
    public void Update_RunsEveryTickByDefault()
    {
        var registry = new SystemRegistry();
        var system = new FakeSystem("EveryTick", order: 100);
        registry.Register(system);

        registry.Update(new SimulationContext(1, 0.05f));
        registry.Update(new SimulationContext(2, 0.05f));

        Assert.Equal(2, system.Runs);
    }

    [Fact]
    public void Update_RespectsCadenceInterval()
    {
        var registry = new SystemRegistry();
        var system = new FakeSystem("EveryThird", order: 100, cadence: new Cadence(3));
        registry.Register(system);

        for (var tick = 1; tick <= 6; tick++)
            registry.Update(new SimulationContext(tick, 0.05f));

        Assert.Equal(2, system.Runs);
        Assert.Equal(6, system.LastTick);
    }

    [Fact]
    public void CadenceOffset_ShiftsPhaseAfterOneFullInterval()
    {
        var cadence = new Cadence(100, 2);

        Assert.False(cadence.ShouldRun(2));
        Assert.False(cadence.ShouldRun(99));
        Assert.False(cadence.ShouldRun(100));
        Assert.True(cadence.ShouldRun(102));
        Assert.True(cadence.ShouldRun(202));
    }

    [Fact]
    public void Update_StaggeredCadence_PassesLogicalBoundaryTick()
    {
        var registry = new SystemRegistry();
        var system = new FakeSystem("Staggered", order: 100, cadence: new Cadence(10, 2));
        registry.Register(system);

        for (var tick = 1; tick <= 12; tick++)
            registry.Update(new SimulationContext(tick, 1f / 20f));

        Assert.Equal(1, system.Runs);
        Assert.Equal(10, system.LastTick);
    }

    private sealed class FakeSystem : ISimulationSystem
    {
        public FakeSystem(string name, int order, Cadence? cadence = null)
        {
            Name = name;
            Order = order;
            Cadence = cadence ?? Cadence.EveryTick;
        }

        public string Name { get; }
        public int Order { get; }
        public Cadence Cadence { get; }
        public int Runs { get; private set; }
        public long LastTick { get; private set; }

        public void Update(in SimulationContext context)
        {
            Runs++;
            LastTick = context.Tick;
        }
    }
}
