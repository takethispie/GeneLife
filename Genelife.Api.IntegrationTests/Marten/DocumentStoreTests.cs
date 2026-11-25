using Genelife.Api.Models;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Marten;

[Trait("Category", "Integration")]
public sealed class DocumentStoreTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public DocumentStoreTests(TestApplicationFactory fixture)
    {
        factory = fixture;
    }

    [Fact]
    public async Task Query_genosdocument_returns_empty_on_fresh_database()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var genoses = await session.Query<GenosDocument>().ToListAsync();
        Assert.NotNull(genoses);
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
    public async Task Store_and_query_genosdocument_roundtrip()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var doc = new GenosDocument
        {
            Id = Guid.NewGuid(),
            NetworkId = 42,
            Name = "TestGenos",
            Age = 25,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        session.Store(doc);
        await session.SaveChangesAsync();

        var loaded = await session.LoadAsync<GenosDocument>(doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal("TestGenos", loaded!.Name);
        Assert.Equal(42L, loaded.NetworkId);
    }
}