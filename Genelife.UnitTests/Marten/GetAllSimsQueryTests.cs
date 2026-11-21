using Genelife.Api.Models;
using Genelife.Api.Services;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Marten;

public sealed class GetAllSimsQueryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public GetAllSimsQueryTests(WebApplicationFactory<Program> fixture)
    {
        factory = fixture;
    }

    [Fact(Skip = "Requires a running Postgres instance with populated SimDocuments")]
    public async Task GetAllSims_returns_from_marten()
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        for (int i = 0; i < 10; i++)
        {
            session.Store(new SimDocument
            {
                Id = Guid.NewGuid(),
                NetworkId = (ulong)(i + 1),
                Name = $"Sim{i}",
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

        var manager = factory.Services.GetRequiredService<SimulationManager>();
        var sims = manager.GetAllSims();

        Assert.Equal(10, sims.Count);
        Assert.Contains(sims, s => s.Name == "Sim0");
    }
}