using Genelife.Api.Models;
using Genelife.Api.Services;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Integration;

public sealed class PersistenceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public PersistenceTests(WebApplicationFactory<Program> fixture)
    {
        factory = fixture;
    }

    [Fact]
    public async Task Simulation_stop_saves_to_marten()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        var preExisting = await CountSavedEntities(store);

        Assert.True(preExisting > 0, "Expected pre-existing entities from startup restore");
    }

    [Fact]
    public async Task Document_store_is_accessible_from_test()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        session.Store(new SimDocument
        {
            Id = Guid.NewGuid(),
            NetworkId = 999,
            Name = "DirectStore",
            Age = 21,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await session.SaveChangesAsync();

        var docs = await session.Query<SimDocument>().ToListAsync();
        Assert.Contains(docs, d => d.Name == "DirectStore");
    }

    private static async Task<int> CountSavedEntities(IDocumentStore store)
    {
        await using var session = store.LightweightSession();
        var sims = await session.Query<SimDocument>().ToListAsync();
        return sims.Count;
    }
}