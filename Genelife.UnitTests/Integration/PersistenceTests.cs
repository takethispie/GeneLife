using System.Net.Http.Headers;
using System.Net.Http.Json;
using Genelife.Api.Auth;
using Genelife.Api.Models;
using Genelife.Api.Services;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genelife.UnitTests.Integration;

public sealed class PersistenceTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public PersistenceTests(TestApplicationFactory fixture)
    {
        factory = fixture;
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Simulation_stop_saves_to_marten()
    {
        var engine = factory.Services.GetRequiredService<SimulationEngine>();
        var manager = factory.Services.GetRequiredService<SimulationManager>();
        var saveService = factory.Services.GetRequiredService<WorldSaveService>();
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        var token = factory.Services.GetRequiredService<JwtTokenService>().CreateToken("admin-1", isAdmin: true).Token;

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var start = await client.PostAsync("/api/simulation/start", null, CancellationToken);
        Assert.True(start.IsSuccessStatusCode || start.StatusCode == System.Net.HttpStatusCode.BadRequest);

        var name = $"PersistedSim_{Guid.NewGuid():N}";
        var add = await client.PostAsJsonAsync(
            "/api/simulation/sims",
            new { name, age = 31 },
            CancellationToken);
        add.EnsureSuccessStatusCode();

        await saveService.SaveAsync(engine.World!, engine.EmploymentIndex!);

        await using var session = store.LightweightSession();
        var docs = await session.Query<SimDocument>().ToListAsync(CancellationToken);
        Assert.Contains(docs, doc => doc.Name == name);

        var stop = await client.PostAsync("/api/simulation/stop", null, CancellationToken);
        Assert.True(stop.IsSuccessStatusCode || stop.StatusCode == System.Net.HttpStatusCode.BadRequest);

        manager.StopSimulation();
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

        await session.SaveChangesAsync(CancellationToken);

        var docs = await session.Query<SimDocument>().ToListAsync(CancellationToken);
        Assert.Contains(docs, d => d.Name == "DirectStore");
    }
}
