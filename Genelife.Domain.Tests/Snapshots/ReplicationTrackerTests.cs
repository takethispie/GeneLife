using Genelife.Api.Snapshots;

namespace Genelife.UnitTests.Snapshots;

public class ReplicationTrackerTests
{
    [Fact]
    public void Invalidate_survives_a_late_acknowledgement()
    {
        var tracker = new ReplicationTracker();
        tracker.Acknowledge("connection-1", "player-1", tick: 10);
        tracker.Invalidate("connection-1");

        tracker.Acknowledge("connection-1", "player-1", tick: 9);

        Assert.True(tracker.NeedsFullSnapshot("connection-1"));
        Assert.Equal(9, tracker.GetAcknowledgedTick("connection-1"));

        tracker.MarkFullSnapshotSent("connection-1", tick: 20);

        Assert.False(tracker.NeedsFullSnapshot("connection-1"));
        Assert.Equal(20, tracker.GetAcknowledgedTick("connection-1"));
    }

    [Fact]
    public void Remove_clears_a_pending_full_snapshot()
    {
        var tracker = new ReplicationTracker();
        tracker.Invalidate("connection-1");

        tracker.Remove("connection-1");

        Assert.False(tracker.NeedsFullSnapshot("connection-1"));
    }

    [Fact]
    public void RestoreForConnection_starts_without_a_pending_full_snapshot()
    {
        var tracker = new ReplicationTracker();
        tracker.Acknowledge("connection-1", "player-1", tick: 10);
        tracker.Invalidate("connection-2");

        tracker.RestoreForConnection("connection-2", "player-1");

        Assert.False(tracker.NeedsFullSnapshot("connection-2"));
        Assert.Equal(10, tracker.GetAcknowledgedTick("connection-2"));
    }
}
