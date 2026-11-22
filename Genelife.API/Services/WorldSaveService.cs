using Arch.Core;
using Arch.Core.Extensions;
using Genelife.Api.Models;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Marten;

namespace Genelife.Api.Services;

public sealed record WorldSaveBatch(
    IReadOnlyList<SimDocument> Sims,
    IReadOnlyList<CompanyDocument> Companies);

public class WorldSaveService
{
    private readonly IDocumentStore? documentStore;
    private readonly ILogger<WorldSaveService>? logger;

    public WorldSaveService(IDocumentStore? documentStore = null, ILogger<WorldSaveService>? logger = null)
    {
        this.documentStore = documentStore;
        this.logger = logger;
    }

    public WorldSaveBatch Build(World world, EmploymentIndex employmentIndex)
    {
        var sims = new List<SimDocument>();

        var simQuery = new QueryDescription().WithAll<GenName, Needs, CurrentAction, NetworkId>();
        world.Query(in simQuery, (Entity entity, ref GenName name, ref Needs needs, ref CurrentAction action, ref NetworkId nid) =>
        {
            sims.Add(new SimDocument
            {
                Id = DocumentIds.ForNetworkId(nid.Value),
                NetworkId = (long)nid.Value,
                Name = name.Name,
                Age = world.TryGet<Alive>(entity, out var alive) ? alive.Age : 18,
                Hunger = needs.Hunger,
                Energy = needs.Energy,
                Hygiene = needs.Hygiene,
                Bladder = needs.Bladder,
                CurrentAction = action.Type.ToString(),
                ActionTimeRemaining = action.TimeRemaining,
                IsPlayerControlled = world.Has<PlayerControlled>(entity),
                PlayerId = world.TryGet<PlayerControlled>(entity, out var pc) ? pc.PlayerId : null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        });

        var companies = new List<CompanyDocument>();

        var companyQuery = new QueryDescription().WithAll<CompanyName, NetworkId>();
        world.Query(in companyQuery, (Entity entity, ref CompanyName name, ref NetworkId nid) =>
        {
            companies.Add(new CompanyDocument
            {
                Id = DocumentIds.ForNetworkId(nid.Value),
                NetworkId = (long)nid.Value,
                Name = name.Name,
                EmployeeCount = employmentIndex.CountEmployees(entity),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        });

        return new WorldSaveBatch(sims, companies);
    }

    public async Task SaveAsync(World world, EmploymentIndex employmentIndex)
    {
        if (documentStore == null)
        {
            logger?.LogInformation("No document store available; skipping world save");
            return;
        }

        await SaveBatchAsync(Build(world, employmentIndex));
    }

    public async Task SaveBatchAsync(WorldSaveBatch batch)
    {
        if (documentStore == null)
        {
            logger?.LogInformation("No document store available; skipping world save");
            return;
        }

        await using var session = documentStore.LightweightSession();

        foreach (var doc in batch.Sims)
            session.Store(doc);

        foreach (var doc in batch.Companies)
            session.Store(doc);

        await session.SaveChangesAsync();
        logger?.LogInformation("World saved to Marten: {SimCount} sims, {CompanyCount} companies",
            batch.Sims.Count, batch.Companies.Count);
    }
}
