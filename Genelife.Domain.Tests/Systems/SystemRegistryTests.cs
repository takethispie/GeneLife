using Genelife.Systems;

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
