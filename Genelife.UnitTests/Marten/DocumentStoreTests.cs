using Genelife.Api.Models;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Marten;

public sealed class DocumentStoreTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public DocumentStoreTests(TestApplicationFactory fixture)
    {
        factory = fixture;
    }

    [Fact]
    public async Task Query_simdocument_returns_empty_on_fresh_database()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var sims = await session.Query<SimDocument>().ToListAsync();
        Assert.NotNull(sims);
    }

    [Fact]
    public async Task Query_companydocument_returns_empty_on_fresh_database()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var companies = await session.Query<CompanyDocument>().ToListAsync();
        Assert.NotNull(companies);
    }

    [Fact]
    public async Task Store_and_query_simdocument_roundtrip()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var doc = new SimDocument
        {
            Id = Guid.NewGuid(),
            NetworkId = 42,
            Name = "TestSim",
            Age = 25,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        session.Store(doc);
        await session.SaveChangesAsync();

        var loaded = await session.LoadAsync<SimDocument>(doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal("TestSim", loaded!.Name);
        Assert.Equal(42L, loaded.NetworkId);
    }
}