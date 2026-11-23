using Arch.Core;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Components.Housing;
using Genelife.Enums;
using Genelife.Family;

namespace Genelife.UnitTests.Family;

public class PregnancyDomainTests
{
    [Fact]
    public void StartCreatesNetworkIdActiveEntryAndHistory()
    {
        var context = new Context();
        var carrying = context.Sim(30, Sex.Female, 1);
        var other = context.Sim(31, Sex.Male, 2);

        Assert.True(context.Service.TryStart(
            carrying, other, conceivedTick: 100, dueTick: 1_000, healthAtConception: 95,
            PregnancySource.Conception, new ReproductionOptions(), out var pregnancy, out var error));

        Assert.Equal(PregnancyError.None, error);
        Assert.True(context.World.Has<NetworkId>(pregnancy));
        Assert.True(context.Index.TryGetActive(carrying, out var active));
        Assert.Equal(pregnancy, active);
        Assert.Contains(pregnancy, context.Index.HistoryOf(carrying));
        var data = context.World.Get<Pregnancy>(pregnancy);
        Assert.Equal(100, data.ConceivedTick);
        Assert.Equal(1_000, data.DueTick);
        Assert.Equal(PregnancyStatus.Active, data.Status);
    }

    [Fact]
    public void DuplicateActivePregnancyIsRejected()
    {
        var context = new Context();
        var carrying = context.Sim(30, Sex.Female, 1);
        var other = context.Sim(31, Sex.Male, 2);
        context.Start(carrying, other);

        var started = context.Service.TryStart(
            carrying, other, 100, 1_000, 95, PregnancySource.Conception,
            new ReproductionOptions(), out _, out var error);

        Assert.False(started);
        Assert.Equal(PregnancyError.ActivePregnancy, error);
    }

    [Fact]
    public void MinorAndDependentCarryingParentsAreRejected()
    {
        var context = new Context();
        var minor = context.Sim(16, Sex.Female, 1);
        var other = context.Sim(31, Sex.Male, 2);

        Assert.False(context.Service.TryStart(
            minor, other, 100, 1_000, 95, PregnancySource.Conception,
            new ReproductionOptions(), out _, out var minorError));
        Assert.Equal(PregnancyError.NotAdult, minorError);

        var dependent = context.Sim(30, Sex.Female, 3);
        var household = context.World.Create(new Household { Head = other });
        context.World.Add(dependent, new HouseholdMember(household, HouseholdRole.Child));

        Assert.False(context.Service.TryStart(
            dependent, other, 100, 1_000, 95, PregnancySource.Conception,
            new ReproductionOptions(), out _, out var dependentError));
        Assert.Equal(PregnancyError.Dependent, dependentError);
    }

    [Fact]
    public void ResolveKeepsHistoryAndUpdatesIndex()
    {
        var context = new Context();
        var carrying = context.Sim(30, Sex.Female, 1);
        var other = context.Sim(31, Sex.Male, 2);
        var pregnancy = context.Start(carrying, other, dueTick: 1_000);

        Assert.True(context.Service.Complete(pregnancy, 1_000));

        Assert.False(context.Index.TryGetActive(carrying, out _));
        Assert.Contains(pregnancy, context.Index.HistoryOf(carrying));
        var data = context.World.Get<Pregnancy>(pregnancy);
        Assert.Equal(PregnancyStatus.Completed, data.Status);
        Assert.Equal(1_000, data.ResolvedTick);
    }

    [Fact]
    public void RebuildRestoresActiveAndHistoricalLookups()
    {
        var context = new Context();
        var carrying = context.Sim(30, Sex.Female, 1);
        var other = context.Sim(31, Sex.Male, 2);
        var lost = context.Start(carrying, other, dueTick: 1_000);
        context.Service.Lose(lost, 1_500);
        var active = context.Start(carrying, other, dueTick: 2_000);

        context.Index.Clear();
        context.Index.Rebuild(context.World);

        Assert.True(context.Index.TryGetActive(carrying, out var current));
        Assert.Equal(active, current);
        Assert.Contains(lost, context.Index.HistoryOf(carrying));
        Assert.True(context.Index.IsInLossCooldown(
            carrying, 1_700, new ReproductionOptions { LossCooldownMonths = 3 }, SimulationClock.Default));
    }

    private sealed class Context
    {
        public World World { get; } = World.Create();
        public PregnancyIndex Index { get; } = new();
        public PregnancyService Service { get; }

        public Context()
        {
            Service = new PregnancyService(World, Index);
        }

        public Entity Sim(int age, Sex sex, ulong networkId) =>
            World.Create(
                new GenName($"Sim {networkId}"),
                new Alive(age),
                new Traits { Sex = sex },
                new NetworkId(networkId));

        public Entity Start(Entity carrying, Entity other, long dueTick = 1_000)
        {
            Assert.True(Service.TryStart(
                carrying, other, 100, dueTick, 95, PregnancySource.Conception,
                new ReproductionOptions(), out var pregnancy, out _));
            return pregnancy;
        }
    }
}
