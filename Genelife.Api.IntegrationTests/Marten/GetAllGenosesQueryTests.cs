using Genelife.Api.Models;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Marten;

[Trait("Category", "Integration")]
public sealed class GetAllGenosesQueryTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public GetAllGenosesQueryTests(TestApplicationFactory fixture)
    {
        factory = fixture;
    }

    [Fact]
    public async Task GetAllGenoses_returns_from_marten_store()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        for (int i = 0; i < 10; i++)
        {
            session.Store(new GenosDocument
            {
                Id = Guid.NewGuid(),
                NetworkId = (long)(i + 1),
                Name = $"Genos{i}",
                Age = 20 + i,
                Hunger = 50 + i,
                Energy = 60,
                Hygiene = 70,
                Bladder = 30,
                CurrentAction = "Idle",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await session.SaveChangesAsync();

        var allDocs = await session.Query<GenosDocument>().ToListAsync();
        Assert.True(allDocs.Count >= 10);
        Assert.Contains(allDocs, d => d.Name == "Genos0");
    }
}