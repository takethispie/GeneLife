using Arch.Core;
using Genelife.Api.Persistence;
using Genelife.Api.Repositories;
using Genelife.Api.Services;
using Genelife.Components.Family;
using Genelife.Components.Gen;
using Genelife.Family;

namespace Genelife.UnitTests.Persistence;

public class RelationshipPersistenceTests
{
    [Fact]
    public void RelationshipEdgesRoundTripAndRebuildTheIndex()
    {
        var created = CreateEngine();
        using var engine = created.Engine;
        var firstId = created.FirstId;
        var secondId = created.SecondId;
        var childId = created.ChildId;

        var state = engine.CaptureState(engine.NetworkIds);
        Assert.Equal(2, state.Relationships.Count(r => r.Kind == "Partner"));
        Assert.Equal(2, state.Relationships.Count(r => r.Kind == "Parent"));
        Assert.NotNull(state.RelationshipOptions);
        engine.Shutdown();

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
        Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
        Assert.Contains(second, restored.Relationships.PartnersOf(first));
        Assert.Contains(first, restored.Relationships.ParentsOf(child));
        Assert.Contains(second, restored.Relationships.ParentsOf(child));
        Assert.Empty(RelationshipInvariants.Verify(restored.World!, restored.Relationships));

        var secondState = restored.CaptureState(restored.NetworkIds);
        restored.Shutdown();
        using var restoredAgain = new SimulationEngine();
        restoredAgain.Restore(secondState, restoredAgain.NetworkIds);
        Assert.Equal(6, restoredAgain.Relationships.ComponentCount);
    }

    [Fact]
    public void SchemaSixSavesMigrateWithNoRelationships()
    {
        var created = CreateEngine();
        using var engine = created.Engine;
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        state.SchemaVersion = 6;
        state.Relationships.Clear();
        state.RelationshipOptions = null;
        SnapshotMigrations.Upgrade(state, 6);

        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        Assert.Equal(0, restored.Relationships.ComponentCount);
        Assert.NotNull(state.RelationshipOptions);
    }

    private static (SimulationEngine Engine, ulong FirstId, ulong SecondId, ulong ChildId) CreateEngine()
    {
        var engine = new SimulationEngine();
        engine.Initialize();
        ulong firstId = 0;
        ulong secondId = 0;
        ulong childId = 0;

        engine.ExecuteWithWorld(world =>
        {
            var repository = new SimRepository(world, engine.NetworkIds, engine.Economy);
            firstId = repository.Add("First", 30).Value;
            secondId = repository.Add("Second", 31).Value;
            childId = repository.Add("Child", 10).Value;

            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(firstId), out var first));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(secondId), out var second));
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(childId), out var child));
            world.Get<Traits>(first) = new Traits { Sex = Sex.Female };
            world.Get<Traits>(second) = new Traits { Sex = Sex.Male };
            world.Get<Traits>(child) = new Traits { Sex = Sex.Male };

            var service = engine.RelationshipService!;
            Assert.True(service.TryCreatePair(first, second, RelationshipKind.Partner,
                RelationshipSource.Seed, 1, out _, out _, out _));
            Assert.True(service.TryCreatePair(first, child, RelationshipKind.Parent,
                RelationshipSource.Seed, 1, out _, out _, out _));
            Assert.True(service.TryCreatePair(second, child, RelationshipKind.Parent,
                RelationshipSource.Seed, 1, out _, out _, out _));
            return 0;
        });

        return (engine, firstId, secondId, childId);
    }
}
