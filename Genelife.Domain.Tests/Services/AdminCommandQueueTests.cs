using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Components.Gen;

namespace Genelife.UnitTests.Services;

/// <summary>
/// Legacy admin endpoints need the created network id synchronously. The manager
/// now enqueues a typed admin command and waits for the tick to apply it, so the
/// request thread never mutates the world.
/// </summary>
public class AdminCommandQueueTests
{
    [Fact]
    public void AdminMutations_AreAppliedOnTheTickThread_AndReturnResults()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();

        var manager = new SimulationManager(engine, new InMemorySnapshotStore());
        using var stop = new CancellationTokenSource();
        var ticker = new Thread(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                engine.Tick(1f / 20f);
                Thread.Sleep(2);
            }
        })
        {
            IsBackground = true,
            Name = "test-tick"
        };
        ticker.Start();

        try
        {
            var genosId = manager.AddGenos("QueuedGenos", 30);
            Assert.NotEqual(0ul, genosId);
            Assert.True(engine.TryGetEntity(new NetworkId(genosId), out _));

            manager.SetGenosAction(genosId, "Idle");
            Assert.True(manager.DeleteGenos(genosId));
            Assert.False(engine.TryGetEntity(new NetworkId(genosId), out _));

            var companyId = manager.AddCompany("QueuedCo");
            Assert.NotEqual(0ul, companyId);
        }
        finally
        {
            stop.Cancel();
            ticker.Join(TimeSpan.FromSeconds(5));
            engine.Shutdown();
        }
    }

    private sealed class InMemorySnapshotStore : ISnapshotStore
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public void Save(PersistedWorldState state)
        {
        }

        public PersistedWorldState? LoadLatest() => null;
    }
}
