using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Marten;

public sealed class ProjectionIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public ProjectionIntegrationTests(TestApplicationFactory fixture)
    {
        factory = fixture;
    }

    [Fact]
    public async Task SimCreated_event_creates_SimDocument_via_projection()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        var simCreated = new SimCreated(1, "TestSim", 25);

        await using var session = store.LightweightSession();
        session.Events.StartStream(Guid.NewGuid(), simCreated);
        await session.SaveChangesAsync();

        var docs = await session.Query<SimDocument>().ToListAsync();
        var doc = docs.FirstOrDefault(d => d.NetworkId == 1);

        Assert.NotNull(doc);
        Assert.Equal("TestSim", doc!.Name);
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
    public async Task SimNeedsChanged_event_updates_existing_SimDocument()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        var streamId = Guid.NewGuid();
        var simCreated = new SimCreated(2, "NeedySim", 30);

        // SimulationEventWriter persists create events before update events in
        // the same flush so inline projections can see the new document.
        await using (var createSession = store.LightweightSession())
        {
            createSession.Events.StartStream(streamId, simCreated);
            await createSession.SaveChangesAsync();
        }

        await using (var updateSession = store.LightweightSession())
        {
            updateSession.Events.Append(streamId, new SimNeedsChanged(2, 80, 50, 60, 40));
            await updateSession.SaveChangesAsync();
        }

        await using var readSession = store.LightweightSession();
        var docs = await readSession.Query<SimDocument>().ToListAsync();
        var doc = docs.FirstOrDefault(d => d.NetworkId == 2);

        Assert.NotNull(doc);
        Assert.Equal(80f, doc!.Hunger);
        Assert.Equal(50f, doc.Energy);
    }
}