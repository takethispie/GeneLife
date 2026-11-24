using Arch.Core;
using Genelife.Components.Gen;
using Genelife.Enums;
using Genelife.Systems;

namespace Genelife.UnitTests.Systems;

/// <summary>
/// The every-tick needs/decision/action passes are per-entity and write only
/// their own entity's components. Running them across Arch chunks must produce
/// exactly the same component values as the serial path.
/// </summary>
public class ParallelSystemDeterminismTests
{
    private const int SimCount = 600;
    private const int Ticks = 20;
    private const float DeltaTime = 1f / 20f;

    [Fact]
    public void ParallelEveryTickPasses_MatchSerialResults()
    {
        var serial = Run(parallel: false);
        var parallel = Run(parallel: true);

        Assert.Equal(serial.Count, parallel.Count);
        foreach (var (networkId, expected) in serial)
        {
            Assert.True(parallel.TryGetValue(networkId, out var actual), $"missing sim {networkId}");
            Assert.Equal(expected.Hunger, actual.Hunger);
            Assert.Equal(expected.Energy, actual.Energy);
            Assert.Equal(expected.Hygiene, actual.Hygiene);
            Assert.Equal(expected.Bladder, actual.Bladder);
            Assert.Equal(expected.Action, actual.Action);
            Assert.Equal(expected.TimeRemaining, actual.TimeRemaining);
        }
    }

    private static Dictionary<ulong, Snapshot> Run(bool parallel)
    {
        using var world = World.Create();

        for (var i = 0; i < SimCount; i++)
        {
            world.Create(
                new NetworkId((ulong)(i + 1)),
                new Needs
                {
                    Hunger = 10f + i % 90,
                    Energy = 5f + i % 95,
                    Hygiene = 15f + i % 80,
                    Bladder = 20f + i % 70
                },
                new CurrentAction(ActionType.Idle, 0f));
        }

        var decay = new NeedsDecaySystem(world, parallel);
        var decision = new DecisionSystem(world, parallel);
        var action = new ActionSystem(world, parallel);

        for (var tick = 1; tick <= Ticks; tick++)
        {
            var context = new SimulationContext(tick, DeltaTime);
            ((ISimulationSystem)decay).Update(in context);
            ((ISimulationSystem)decision).Update(in context);
            ((ISimulationSystem)action).Update(in context);
        }

        var result = new Dictionary<ulong, Snapshot>();
        world.Query(in new QueryDescription().WithAll<NetworkId, Needs, CurrentAction>(),
            (ref NetworkId id, ref Needs needs, ref CurrentAction current) =>
            {
                result[id.Value] = new Snapshot(
                    needs.Hunger,
                    needs.Energy,
                    needs.Hygiene,
                    needs.Bladder,
                    current.Type,
                    current.TimeRemaining);
            });
        return result;
    }

    private readonly record struct Snapshot(
        float Hunger,
        float Energy,
        float Hygiene,
        float Bladder,
        ActionType Action,
        float TimeRemaining);
}
