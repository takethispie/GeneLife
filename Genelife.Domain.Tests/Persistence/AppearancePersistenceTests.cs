using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Appearance;
using Genelife.Components.Gen;

namespace Genelife.UnitTests.Persistence;

public class AppearancePersistenceTests
{
    [Fact]
    public void AppearanceGenesRoundTripAndDoubleRestoreIsIdentical()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var playerId = "appearance-player";
        var genosNetworkId = engine.EnsurePlayerGenos(playerId);
        Assert.NotEqual(0UL, genosNetworkId);

        ulong eye = 0;
        ulong hair = 0;
        ulong skin = 0;
        ulong face = 0;
        engine.ExecuteWithWorld(world =>
        {
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos));
            var traits = world.Get<Traits>(genos);
            eye = (ulong)traits.EyeColor;
            hair = (ulong)traits.HairColor;
            skin = (ulong)traits.SkinTone;
            face = (ulong)traits.FaceShape;
            return 0;
        });

        var state = engine.CaptureState(engine.NetworkIds);
        Assert.Equal(16, PersistedWorldState.CurrentSchemaVersion);
        var saved = state.Genoses.Single(genos => genos.NetworkId == genosNetworkId);
        Assert.True(saved.AppearanceMigrated);
        Assert.Equal((int)skin, saved.SkinTone);
        Assert.Equal((int)face, saved.FaceShape);
        Assert.Equal(AppearanceDescriptor.CurrentVersion, saved.AppearanceVersion);

        AppearanceDescriptor before = default;
        engine.ExecuteWithWorld(world =>
        {
            Assert.True(engine.NetworkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos));
            before = AppearanceDerivation.Derive(world.Get<Traits>(genos), world.Get<Alive>(genos).Age, genosNetworkId);
            return 0;
        });

        engine.Shutdown();
        using var restored = new SimulationEngine();
        restored.Restore(state, restored.NetworkIds);

        AppearanceDescriptor after = default;
        restored.ExecuteWithWorld(world =>
        {
            Assert.True(restored.NetworkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos));
            after = AppearanceDerivation.Derive(world.Get<Traits>(genos), world.Get<Alive>(genos).Age, genosNetworkId);
            Assert.Equal((EyeColor)eye, world.Get<Traits>(genos).EyeColor);
            Assert.Equal((HairColor)hair, world.Get<Traits>(genos).HairColor);
            Assert.Equal((SkinTone)skin, world.Get<Traits>(genos).SkinTone);
            Assert.Equal((FaceShape)face, world.Get<Traits>(genos).FaceShape);
            return 0;
        });
        Assert.Equal(before, after);

        var second = restored.CaptureState(restored.NetworkIds);
        restored.Shutdown();
        var secondSaved = second.Genoses.Single(genos => genos.NetworkId == genosNetworkId);
        Assert.Equal(saved.SkinTone, secondSaved.SkinTone);
        Assert.Equal(saved.FaceShape, secondSaved.FaceShape);
        Assert.Equal(saved.AppearanceVersion, secondSaved.AppearanceVersion);
    }

    [Fact]
    public void SchemaTwelveTraitsBackfillDeterministicallyAndKeepExistingTraits()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var genosNetworkId = engine.EnsurePlayerGenos("legacy-appearance");
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        state.SchemaVersion = 12;
        var saved = state.Genoses.Single(genos => genos.NetworkId == genosNetworkId);
        var eye = saved.EyeColor;
        var hair = saved.HairColor;
        saved.AppearanceMigrated = false;
        saved.AppearanceVersion = 0;
        saved.SkinTone = 0;
        saved.FaceShape = 0;

        SnapshotMigrations.Upgrade(state, 12);
        Assert.Equal(16, state.SchemaVersion);

        using var first = new SimulationEngine();
        first.Restore(state, first.NetworkIds);
        AppearanceDescriptor firstDescriptor = default;
        first.ExecuteWithWorld(world =>
        {
            Assert.True(first.NetworkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos));
            var traits = world.Get<Traits>(genos);
            Assert.Equal(eye, (int)traits.EyeColor);
            Assert.Equal(hair, (int)traits.HairColor);
            firstDescriptor = AppearanceDerivation.Derive(traits, world.Get<Alive>(genos).Age, genosNetworkId);
            return 0;
        });
        var firstState = first.CaptureState(first.NetworkIds);
        first.Shutdown();

        using var second = new SimulationEngine();
        second.Restore(firstState, second.NetworkIds);
        AppearanceDescriptor secondDescriptor = default;
        second.ExecuteWithWorld(world =>
        {
            Assert.True(second.NetworkIds.TryGetEntity(new NetworkId(genosNetworkId), out var genos));
            secondDescriptor = AppearanceDerivation.Derive(
                world.Get<Traits>(genos), world.Get<Alive>(genos).Age, genosNetworkId);
            return 0;
        });
        second.Shutdown();

        Assert.Equal(firstDescriptor, secondDescriptor);
    }

    [Fact]
    public void FutureAppearanceVersionFailsLoudly()
    {
        using var engine = new SimulationEngine();
        engine.Initialize();
        var genosNetworkId = engine.EnsurePlayerGenos("future-appearance");
        var state = engine.CaptureState(engine.NetworkIds);
        engine.Shutdown();

        state.Genoses.Single(genos => genos.NetworkId == genosNetworkId).AppearanceVersion =
            AppearanceDescriptor.CurrentVersion + 1;

        using var restored = new SimulationEngine();
        var error = Assert.ThrowsAny<Exception>(() => restored.Restore(state, restored.NetworkIds));
        Assert.Contains("appearance version", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
