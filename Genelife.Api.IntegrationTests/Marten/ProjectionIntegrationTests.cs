using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Marten;

[Trait("Category", "Integration")]
public sealed class ProjectionIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public ProjectionIntegrationTests(TestApplicationFactory fixture)
    {
        factory = fixture;
    }

    [Fact]
    public async Task GenosCreated_event_creates_GenosDocument_via_projection()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        var genosCreated = new GenosCreated(1, "TestGenos", 25);

        await using var session = store.LightweightSession();
        session.Events.StartStream(Guid.NewGuid(), genosCreated);
        await session.SaveChangesAsync();

        var docs = await session.Query<GenosDocument>().ToListAsync();
        var doc = docs.FirstOrDefault(d => d.NetworkId == 1);

        Assert.NotNull(doc);
        Assert.Equal("TestGenos", doc!.Name);
        Assert.Equal(25, doc.Age);
    }

    [Fact]
    public async Task CompanyCreated_event_creates_CompanyDocument_via_projection()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        var companyCreated = new CompanyCreated(1, "TestCorp");

        await using var session = store.LightweightSession();
        session.Events.StartStream(Guid.NewGuid(), companyCreated);
        await session.SaveChangesAsync();

        var docs = await session.Query<CompanyDocument>().ToListAsync();
        var doc = docs.FirstOrDefault(d => d.NetworkId == 1);

        Assert.NotNull(doc);
        Assert.Equal("TestCorp", doc!.Name);
    }

    [Fact]
    public async Task GenosNeedsChanged_event_updates_existing_GenosDocument()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        var streamId = Guid.NewGuid();
        var genosCreated = new GenosCreated(2, "NeedyGenos", 30);

        // SimulationEventWriter persists create events before update events in
        // the same flush so inline projections can see the new document.
        await using (var createSession = store.LightweightSession())
        {
            createSession.Events.StartStream(streamId, genosCreated);
            await createSession.SaveChangesAsync();
        }

        await using (var updateSession = store.LightweightSession())
        {
            updateSession.Events.Append(streamId, new GenosNeedsChanged(2, 80, 50, 60, 40));
            await updateSession.SaveChangesAsync();
        }

        await using var readSession = store.LightweightSession();
        var docs = await readSession.Query<GenosDocument>().ToListAsync();
        var doc = docs.FirstOrDefault(d => d.NetworkId == 2);

        Assert.NotNull(doc);
        Assert.Equal(80f, doc!.Hunger);
        Assert.Equal(50f, doc.Energy);
    }

    [Fact]
    public async Task MapGenerated_creates_MapDocument()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        var mapId = $"test-map-{Guid.NewGuid():N}";
        var generated = new MapGenerated(mapId, 3, 99, 64, 64, 1f, 10);

        await using var session = store.LightweightSession();
        session.Events.StartStream(Guid.NewGuid(), generated);
        await session.SaveChangesAsync();

        var doc = await session.LoadAsync<MapDocument>(mapId);
        Assert.NotNull(doc);
        Assert.Equal(3, doc!.Version);
        Assert.Equal(-32, doc.MinX);
        Assert.Equal(31, doc.MaxX);
    }

    [Fact]
    public async Task BuildingPlaced_upserts_and_BuildingRemoved_deletes_document()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        var mapId = $"test-map-{Guid.NewGuid():N}";
        var networkId = (ulong)Random.Shared.NextInt64(1_000_000, 9_000_000);
        var placed = new BuildingPlaced(mapId, 4, networkId, "Residential", "Test Block", "Central", 2, 3, 4, 3, 5, 6, 20);

        var streamId = Guid.NewGuid();
        await using (var session = store.LightweightSession())
        {
            session.Events.StartStream(streamId, new MapGenerated(mapId, 4, 1, 64, 64, 1f, 20), placed);
            await session.SaveChangesAsync();
        }

        await using (var update = store.LightweightSession())
        {
            update.Events.Append(streamId, placed with { Name = "Updated Block", MapVersion = 5 });
            await update.SaveChangesAsync();
        }

        await using (var read = store.LightweightSession())
        {
            var doc = await read.Query<BuildingDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
            Assert.NotNull(doc);
            Assert.Equal("Updated Block", doc!.Name);
            Assert.Equal(4, doc.Width);
            Assert.Equal(5, doc.MapVersion);
        }

        await using (var remove = store.LightweightSession())
        {
            remove.Events.Append(streamId, new BuildingRemoved(mapId, 6, networkId, 30));
            await remove.SaveChangesAsync();
        }

        await using var verify = store.LightweightSession();
        var deleted = await verify.Query<BuildingDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task MapGenerated_for_an_existing_map_overwrites_layout_metadata()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        var mapId = $"test-map-{Guid.NewGuid():N}";
        var streamId = Guid.NewGuid();

        await using (var session = store.LightweightSession())
        {
            session.Events.StartStream(streamId, new MapGenerated(mapId, 2, 7, 64, 64, 1f, 10));
            await session.SaveChangesAsync();
        }

        await using (var update = store.LightweightSession())
        {
            update.Events.Append(streamId, new MapGenerated(mapId, 5, 99, 128, 96, 1f, 20));
            await update.SaveChangesAsync();
        }

        await using var read = store.LightweightSession();
        var doc = await read.LoadAsync<MapDocument>(mapId);
        Assert.NotNull(doc);
        Assert.Equal(5, doc!.Version);
        Assert.Equal(99, doc.Seed);
        Assert.Equal(128, doc.Width);
        Assert.Equal(96, doc.Height);
        Assert.Equal(-64, doc.MinX);
        Assert.Equal(63, doc.MaxX);
        Assert.Equal(-48, doc.MinY);
        Assert.Equal(47, doc.MaxY);
    }
}