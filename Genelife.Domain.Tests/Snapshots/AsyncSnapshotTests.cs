using Genelife.Api.Services;
using Genelife.Api.Snapshots;
using Genelife.Components.Gen;
using Genelife.Enums;

namespace Genelife.UnitTests.Snapshots;

/// <summary>
/// The hosted loop enables async snapshots so snapshot construction does not
/// count against the tick budget. The consumer must still publish a consistent
/// latest snapshot and stop cleanly on shutdown.
/// </summary>
public class AsyncSnapshotTests
{
    [Fact]
    public void AsyncSnapshots_PublishAfterTick_AndStopOnShutdown()
    {
        using var engine = new SimulationEngine();
        engine.AsyncSnapshots = true;
        engine.Initialize();

        try
        {
            engine.ExecuteWithWorld(world =>
            {
                world.Create(
                    new GenName("AsyncSim"),
                    new Needs { Hunger = 50f },
                    new CurrentAction(ActionType.Idle, 0f),
                    new Alive(20));
                return true;
            });

            engine.Tick(1f / 20f);

            WorldSnapshot? snapshot = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                snapshot = engine.GetLatestSnapshot();
                if (snapshot is not null)
                    break;
                Thread.Sleep(10);
            }

            Assert.NotNull(snapshot);
            Assert.Contains(snapshot.Sims, sim => sim.Name == "AsyncSim");
        }
        finally
        {
            engine.Shutdown();
        }

        Assert.Null(engine.GetLatestSnapshot());
    }
}
