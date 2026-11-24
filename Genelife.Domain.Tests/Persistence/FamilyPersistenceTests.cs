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

public class FamilyPersistenceTests
{

    [Fact]
    public void CapturedFamilyEdgesAlwaysPointAtCapturedSims()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        engine.ExecuteWithWorld(world =>
        {
            var generator = new PopulationGenerator(
                world, engine.NetworkIds, engine.Economy!, engine.EmploymentIndex!,
                relationshipIndex: engine.Relationships,
                relationshipService: engine.RelationshipService!,
                relationshipOptions: engine.RelationshipOptions);
            generator.Generate(200, seed: 21);
            return 0;
        });
        var ticks = engine.Economy!.Clock.TicksPerYear;
        for (var i = 0; i < ticks; i++)
            engine.Tick(1f / 20f);

        var state = engine.CaptureState(engine.NetworkIds);
        var ids = state.Sims.Select(sim => sim.NetworkId).ToHashSet();
        var dangling = state.Relationships
            .Where(rel => !ids.Contains(rel.FromSimNetworkId) || !ids.Contains(rel.ToSimNetworkId))
            .Select(rel => $"{rel.NetworkId}:{rel.FromSimNetworkId}->{rel.ToSimNetworkId}:{rel.Kind}")
            .ToList();
        var missingIds = state.Relationships
            .SelectMany(rel => new[] { rel.FromSimNetworkId, rel.ToSimNetworkId })
            .Where(id => !ids.Contains(id))
            .Distinct()
            .ToList();
        var diagnostics = new List<string>();
        foreach (var missingId in missingIds)
        {
            var found = engine.NetworkIds.TryGetEntity(new NetworkId(missingId), out var entity);
            var alive = found && engine.World!.IsAlive(entity);
            diagnostics.Add($"{missingId}:found={found},alive={alive}," +
                $"genName={alive && engine.World!.Has<GenName>(entity)}," +
                $"needs={alive && engine.World!.Has<Needs>(entity)}," +
                $"action={alive && engine.World!.Has<CurrentAction>(entity)}," +
                $"dead={alive && engine.World!.Has<Dead>(entity)}");
        }
        Assert.True(dangling.Count == 0,
            $"dangling={dangling.Count} ids={ids.Count} sims={state.Sims.Count} relationships={state.Relationships.Count} " +
            $"diagnostics=[{string.Join(";", diagnostics)}] sample={string.Join(",", dangling.Take(10))}");
    }

    [Fact]
    public void StateCareRoundTripsAndDoubleRestoreIsExact()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong childId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);
            childId = repository.Add("Ward", 7).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
            world.Add(child, new StateCare
            {
                SinceTick = 12,
                CaseWorker = Entity.Null,
                MonthlyCost = Money.FromUnits(30m)
            });
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        var care = Assert.Single(state.StateCare);
        Assert.Equal(childId, care.ChildNetworkId);
        Assert.Equal(30_00, care.MonthlyCostCents);
        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);
        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(childId), out var restoredChild));
        Assert.True(restored.World!.Has<StateCare>(restoredChild));
        Assert.Equal(12, restored.World.Get<StateCare>(restoredChild).SinceTick);

        var second = restored.CaptureState(restored.NetworkIds);
        restored.Shutdown();

        using var restoredAgain = new SimulationEngine();
        restoredAgain.Restore(second, restoredAgain.NetworkIds);
        var third = restoredAgain.CaptureState(restoredAgain.NetworkIds);

        Assert.Equal(second.StateCare.Count, third.StateCare.Count);
        Assert.Equal(second.StateCare[0].ChildNetworkId, third.StateCare[0].ChildNetworkId);
        Assert.Equal(second.StateCare[0].SinceTick, third.StateCare[0].SinceTick);
        Assert.Equal(second.StateCare[0].MonthlyCostCents, third.StateCare[0].MonthlyCostCents);
    }

    [Fact]
    public void SchemaNineSavesGainAnEmptyStateCareList()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        state.SchemaVersion = 9;
        state.StateCare.Clear();
        SnapshotMigrations.Upgrade(state, 9);

        Assert.NotNull(state.StateCare);
        Assert.Empty(state.StateCare);
        Assert.Equal(PersistedWorldState.CurrentSchemaVersion, state.SchemaVersion);
    }

    [Fact]
    public void DanglingRelationshipIsPrunedOnRestore()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong simId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);
            simId = repository.Add("Only", 30).Value;
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        state.Relationships.Add(new PersistedRelationship
        {
            NetworkId = 9_999_999,
            FromSimNetworkId = simId,
            ToSimNetworkId = 8_888_888,
            Kind = "Partner",
            Source = "Admin",
            SinceTick = 1
        });
        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(simId), out _));
        Assert.Empty(restored.BuildFamilyRelationships().Relationships);
    }

    [Fact]
    public void UnknownRelationshipKindFailsLoudly()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong firstId = 0;
        ulong secondId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("First", 30).Value;
            secondId = repository.Add("Second", 31).Value;
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        state.Relationships.Add(new PersistedRelationship
        {
            NetworkId = 9_999_998,
            FromSimNetworkId = firstId,
            ToSimNetworkId = secondId,
            Kind = "Cousin",
            Source = "Admin",
            SinceTick = 1
        });
        engine.Shutdown();

        using var restored = new SimulationEngine();
        var error = Assert.Throws<InvalidOperationException>(() => restored.Restore(state, restored.NetworkIds));
        Assert.Contains("unknown kind", error.Message);
    }

    [Fact]
    public void ResolvedPregnancyWithMissingParentIsPruned()
    {
        var (state, _) = CreatePregnancyState(active: false);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.Empty(restored.BuildPregnancyPage().Pregnancies);
    }

    [Fact]
    public void ActivePregnancyWithMissingParentStillFailsLoudly()
    {
        var (state, _) = CreatePregnancyState(active: true);

        using var restored = new SimulationEngine();
        var error = Assert.Throws<InvalidOperationException>(() => restored.Restore(state, restored.NetworkIds));
        Assert.Contains("Active pregnancy", error.Message);
    }

    private static (PersistedWorldState State, ulong CarryingId) CreatePregnancyState(bool active)
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        ulong carryingId = 0;
        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);
            carryingId = repository.Add("Carrier", 30).Value;
            var otherId = repository.Add("Other", 31).Value;
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(carryingId), out var carrying));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(otherId), out var other));
            var dueTick = engine.Economy!.Clock.TicksPerYear;
            Assert.True(engine.PregnancyService!.TryStart(
                carrying, other, 0, dueTick, 100,
                PregnancySource.Conception, engine.ReproductionOptions,
                out _, out var error), error.ToString());
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        state.Pregnancies[0].Status = active ? "Active" : "Completed";
        state.Sims.RemoveAll(sim => sim.NetworkId == carryingId);
        engine.Shutdown();
        return (state, carryingId);
    }
}
