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

    [Fact]
    public async Task Store_and_query_companydocument_roundtrip_preserves_financial_fields()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();

        var doc = new CompanyDocument
        {
            Id = Guid.NewGuid(),
            NetworkId = 987654,
            Name = "RoundTrip Corp",
            EmployeeCount = 7,
            OfficeCount = 3,
            Balance = 12_345.67m,
            ReservedForPayroll = 2_500.25m,
            Arrears = 450.50m,
            RevenueLastCycle = 9_876.54m,
            WagesLastCycle = 6_543.21m,
            Solvency = "Tight",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await using (var writeSession = store.LightweightSession())
        {
            writeSession.Store(doc);
            await writeSession.SaveChangesAsync();
        }

        await using var readSession = store.LightweightSession();
        var loaded = await readSession.LoadAsync<CompanyDocument>(doc.Id);

        Assert.NotNull(loaded);
        Assert.Equal(7, loaded!.EmployeeCount);
        Assert.Equal(3, loaded.OfficeCount);
        Assert.Equal(12_345.67m, loaded.Balance);
        Assert.Equal(2_500.25m, loaded.ReservedForPayroll);
        Assert.Equal(450.50m, loaded.Arrears);
        Assert.Equal(9_876.54m, loaded.RevenueLastCycle);
        Assert.Equal(6_543.21m, loaded.WagesLastCycle);
        Assert.Equal("Tight", loaded.Solvency);

        var status = loaded.ToCompanyStatus();
        Assert.Equal((ulong)987654, status.NetworkId);
        Assert.Equal("RoundTrip Corp", status.Name);
        Assert.Equal(7, status.EmployeeCount);
        Assert.Equal(3, status.OfficeCount);
        Assert.Equal(12_345.67m, status.Balance);
        Assert.Equal(2_500.25m, status.ReservedForPayroll);
        Assert.Equal(450.50m, status.Arrears);
        Assert.Equal(9_876.54m, status.RevenueLastCycle);
        Assert.Equal(6_543.21m, status.WagesLastCycle);
        Assert.Equal("Tight", status.Solvency);
    }
}