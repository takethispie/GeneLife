using Arch.Core;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Finance;
using Genelife.Components.Gen;
using Genelife.Family;
using Genelife.Values;

namespace Genelife.UnitTests.Persistence;

public class PregnancyRestoreTests
{
    [Fact]
    public void ActivePregnancyAndBirthRecordRoundTrip()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong firstId = 0;
        ulong secondId = 0;

        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("Carrying", 30).Value;
            secondId = repository.Add("Other", 31).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
            world.Get<Traits>(first) = new Traits { Sex = Sex.Female };
            world.Get<Traits>(second) = new Traits { Sex = Sex.Male };
            world.Add(first, new BirthRecord { PregnancyNetworkId = 99, BornTick = 7 });
            Assert.True(engine.PregnancyService!.TryStart(
                first, second, 100, 1_000, 90, PregnancySource.Conception,
                engine.ReproductionOptions, out _, out _));
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        Assert.Single(state.Pregnancies);
        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(firstId), out var restoredFirst));
        Assert.True(restored.World!.IsAlive(restoredFirst));
        Assert.True(restored.World!.Has<BirthRecord>(restoredFirst));
        Assert.Equal(99UL, restored.World.Get<BirthRecord>(restoredFirst).PregnancyNetworkId);
        Assert.Single(restored.BuildPregnancies());
        var pregnancy = restored.BuildPregnancies()[0];
        Assert.Equal(100, pregnancy.ConceivedTick);
        Assert.Equal(1_000, pregnancy.DueTick);
        Assert.Equal("Active", pregnancy.Status);
        Assert.True(restored.Pregnancies.TryGetActive(
            restoredFirst, out _));
    }


    [Fact]
    public void TrustAccountsRoundTripAndStayInMoneySupply()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        var before = Money.Zero;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new GenosRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Child", 8).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
            Assert.NotEqual(Entity.Null, engine.Trusts!.Create(
                child, Entity.Null, Money.FromUnits(125m), 0, 5, 18L * engine.Economy!.Clock.TicksPerYear));
            before = engine.Economy.TotalMoneySupply();
            return 0;
        });
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);
        var tracker = 0;
        var balance = Money.Zero;
        restored.ExecuteWithWorld(world =>
        {
            world.Query(in new QueryDescription().WithAll<TrustAccount>(),
                (Entity _, ref TrustAccount trust) =>
                {
                    tracker++;
                    balance += trust.Balance;
                });
            return 0;
        });

        Assert.Equal(1, tracker);
        Assert.Equal(Money.FromUnits(125m), balance);
        Assert.Equal(before, restored.Economy!.TotalMoneySupply());
    }

    [Fact]
    public void SchemaSevenSavesMigrateWithoutPregnancies()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();
        state.SchemaVersion = 7;
        state.Pregnancies.Clear();
        state.ReproductionOptions = null;

        SnapshotMigrations.Upgrade(state, 7);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.Empty(restored.BuildPregnancies());
        Assert.NotNull(state.ReproductionOptions);
    }
}
