using Genelife.Api.Digest;
using Genelife.Messages.Events;

namespace Genelife.UnitTests.Digest;

/// <summary>
/// Unit coverage for the Town bell digest projection (GDD ch.08 section 8.5(6)):
/// the server-computed grouping key, the rolling window, the connected-entity
/// filter and the ambient-event suppression.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TownBellDigestProjectionTests
{
    private static TownBellDigestService NewService(int windowTicks = 100, int capacity = 100_000) =>
        new(new DigestOptions { WindowTicks = windowTicks, BufferCapacity = capacity });

    private static PlayerDigestScope Scope(params string[] refs) =>
        new("player-1", 1, refs);

    [Fact]
    public void Classifier_uses_the_genos_subject_for_a_genos_event()
    {
        Assert.True(DigestEventClassifier.TryClassify(new GenosActionCompleted(42, "Work"), 7, out var digest));
        Assert.Equal("GenosActionCompleted", digest.EventKind);
        Assert.Equal("genos", digest.SubjectEntityType);
        Assert.Equal(42UL, digest.SubjectEntityId);
        Assert.Equal(7, digest.Tick); // no Tick property → server fallback tick
    }

    [Fact]
    public void Classifier_prefers_the_events_own_tick_when_it_carries_one()
    {
        Assert.True(DigestEventClassifier.TryClassify(
            new LifeStageChanged(5, "Child", "Work", "age", 99), 1, out var digest));
        Assert.Equal(99, digest.Tick);
        Assert.Equal("genos", digest.SubjectEntityType);
    }

    [Fact]
    public void Classifier_reads_a_household_subject()
    {
        Assert.True(DigestEventClassifier.TryClassify(new RentPaid(9, 3, 2, 1000, 55), 1, out var digest));
        Assert.Equal("household", digest.SubjectEntityType);
        Assert.Equal(9UL, digest.SubjectEntityId);
        Assert.Equal("RentPaid", digest.EventKind);
    }

    [Fact]
    public void Classifier_suppresses_the_ambient_per_tick_event()
    {
        Assert.False(DigestEventClassifier.TryClassify(
            new GenosNeedsChanged(5, 1f, 1f, 1f, 1f), 1, out _));
    }

    [Fact]
    public void Classifier_suppresses_tick_and_metric_aggregates()
    {
        Assert.False(DigestEventClassifier.TryClassify(new TickCompleted(10, 2, 1.0), 1, out _));
        Assert.False(DigestEventClassifier.TryClassify(
            new EconomySnapshotTaken(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0m, 0m, 0m, 0m, 0), 1, out _));
    }

    [Fact]
    public void Digest_groups_by_event_kind_and_subject_within_the_window()
    {
        var service = NewService(windowTicks: 100);

        service.Record(new DigestEvent("GenosActionCompleted", "genos", 1, 150));
        service.Record(new DigestEvent("GenosActionCompleted", "genos", 1, 170));
        service.Record(new DigestEvent("GenosActionCompleted", "genos", 2, 160));
        service.Record(new DigestEvent("RentPaid", "household", 9, 180));
        // Outside the rolling window (as of 180, window 100 → from 80).
        service.Record(new DigestEvent("RentPaid", "household", 9, 10));

        var scope = Scope("genos:1", "genos:2", "household:9");
        var digest = service.Build(scope);

        Assert.Equal(180, digest.AsOfTick);
        Assert.Equal(80, digest.WindowStartTick);
        Assert.Equal(3, digest.EntryCount);

        var genosOne = digest.Entries.Single(e => e.SubjectEntityId == 1);
        Assert.Equal(2, genosOne.Count);
        Assert.Equal(150, genosOne.FirstTick);
        Assert.Equal(170, genosOne.LastTick);

        var household = digest.Entries.Single(e => e.SubjectEntityType == "household");
        Assert.Equal(1, household.Count);
        Assert.Equal(180, household.FirstTick);

        // Highest count first.
        Assert.Equal(1UL, digest.Entries[0].SubjectEntityId);
    }

    [Fact]
    public void Digest_excludes_events_for_entities_outside_the_players_scope()
    {
        var service = NewService(windowTicks: 100);
        service.Record(new DigestEvent("GenosActionCompleted", "genos", 1, 150));
        service.Record(new DigestEvent("GenosActionCompleted", "genos", 77, 150)); // not connected

        var digest = service.Build(Scope("genos:1"));

        Assert.Single(digest.Entries);
        Assert.Equal(1UL, digest.Entries[0].SubjectEntityId);
    }

    [Fact]
    public void Digest_buffer_is_bounded_and_drops_the_oldest_events_first()
    {
        var service = NewService(windowTicks: 1000, capacity: 3);
        service.Record(new DigestEvent("A", "genos", 1, 10));
        service.Record(new DigestEvent("B", "genos", 1, 11));
        service.Record(new DigestEvent("C", "genos", 1, 12));
        service.Record(new DigestEvent("D", "genos", 1, 13)); // evicts "A" at tick 10

        var status = service.BuildStatus();
        Assert.Equal(3, status.BufferedEvents);
        Assert.Equal(11, status.OldestTick);
        Assert.Equal(13, status.NewestTick);
        Assert.DoesNotContain(status.Entries, e => e.EventKind == "A");
    }

    [Fact]
    public void Reset_clears_the_buffer()
    {
        var service = NewService();
        service.Record(new DigestEvent("A", "genos", 1, 10));
        service.Reset(0);
        Assert.Equal(0, service.BuildStatus().BufferedEvents);
    }
}
