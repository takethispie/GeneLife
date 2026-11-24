using Arch.Core;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Persistence;

public class TraitPersistenceTests
{
    [Fact]
    public void CaptureAndRestorePreserveTraitsExactly()
    {
        var options = new GeneticsOptions { MutationRate = 0.1m };
        var (state, ids, traits) = CreateCapturedState(options);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        foreach (var (networkId, expected) in traits)
        {
            Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(networkId), out var entity));
            Assert.Equal(expected, restored.World!.Get<Traits>(entity));
        }

        Assert.NotNull(state.GeneticsOptions);
        Assert.Equal(AttractionCoefficients.CurrentVersion, state.AttractionModelVersion);
    }

    [Fact]
    public void MigrationBackfillsTraitsDeterministicallyAndIsStableOnSecondRestore()
    {
        var options = new GeneticsOptions { MutationRate = 0.07m };
        var (state, ids, _) = CreateCapturedState(options);

        state.SchemaVersion = 5;
        foreach (var sim in state.Sims)
        {
            sim.TraitsMigrated = false;
            sim.EyeColor = 0;
            sim.HairColor = 0;
            sim.Handedness = 0;
            sim.Morphotype = 0;
            sim.Aptitude = 0;
            sim.HeightPotential = 0;
            sim.Sex = 0;
            sim.Temperament = 0;
        }

        SnapshotMigrations.Upgrade(state, 5);

        using var first = new SimulationEngine();
        first.Restore(state, first.NetworkIds);

        var migrated = new Dictionary<ulong, Traits>();
        foreach (var sim in state.Sims)
        {
            Assert.True(first.NetworkIds.TryGetEntity(new NetworkId(sim.NetworkId), out var entity));
            var traits = first.World!.Get<Traits>(entity);
            TraitValidation.EnsureValid(traits, sim.NetworkId);
            migrated[sim.NetworkId] = traits;

            var expected = TraitsGenerator.Create(
                state.RandomSeed, sim.NetworkId, TraitSource.Migration, first.Economy!.Genetics);
            Assert.Equal(expected, traits);
        }

        var secondState = first.CaptureState(first.NetworkIds);
        first.Shutdown();

        using var second = new SimulationEngine();
        second.Restore(secondState, second.NetworkIds);

        foreach (var (networkId, expected) in migrated)
        {
            Assert.True(second.NetworkIds.TryGetEntity(new NetworkId(networkId), out var entity));
            Assert.Equal(expected, second.World!.Get<Traits>(entity));
        }
    }

    [Fact]
    public void UndefinedStoredTraitFailsRestoreWithDescriptiveError()
    {
        var (state, _, _) = CreateCapturedState(new GeneticsOptions());
        state.Sims[0].EyeColor = 200;

        using var restored = new SimulationEngine();
        var error = Assert.Throws<InvalidOperationException>(
            () => restored.Restore(state, restored.NetworkIds));

        Assert.Contains(state.Sims[0].NetworkId.ToString(), error.Message);
        Assert.Contains("EyeColor", error.Message);
    }

    private static (PersistedWorldState State, IEnumerable<ulong> Ids, Dictionary<ulong, Traits> Traits) CreateCapturedState(
        GeneticsOptions options)
    {
        var engine = new SimulationEngine();
        engine.Initialize(geneticsOptions: options);
        var world = engine.World!;
        var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);

        var ids = new List<ulong>
        {
            repository.Add("Alice", 30).Value,
            repository.Add("Bob", 32).Value,
            repository.Add("Cara", 12).Value
        };

        var traits = new Dictionary<ulong, Traits>();
        foreach (var id in ids)
        {
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(id), out var entity));
            traits[id] = world.Get<Traits>(entity);
            Assert.True(TraitValidation.IsValid(traits[id]));
        }

        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();
        return (state, ids, traits);
    }
}
