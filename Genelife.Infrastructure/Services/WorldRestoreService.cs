using Arch.Core;
using Genelife.Api.Models;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Marten;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Services;

public class WorldRestoreService
{
    private readonly IDocumentStore? documentStore;
    private readonly ILogger<WorldRestoreService>? logger;

    public WorldRestoreService(IDocumentStore? documentStore = null, ILogger<WorldRestoreService>? logger = null)
    {
        this.documentStore = documentStore;
        this.logger = logger;
    }

    public async Task<(List<EntityBlueprint> entities, int genosCount, int companyCount)> RestoreAsync(
        NetworkIdRegistry networkIds,
        EmploymentIndex employmentIndex)
    {
        var blueprints = new List<EntityBlueprint>();

        if (documentStore == null)
        {
            logger?.LogInformation("No document store available; skipping world restore");
            return (blueprints, 0, 0);
        }

        try
        {
            await using var session = documentStore.LightweightSession();

            var genosDocs = await session.Query<GenosDocument>().ToListAsync();
            var companyDocs = await session.Query<CompanyDocument>().ToListAsync();

            foreach (var genos in genosDocs)
            {
                blueprints.Add(new EntityBlueprint(
                    EntityType.Genos,
                    (ulong)genos.NetworkId,
                    genos.Name,
                    genos.Age,
                    genos.Hunger,
                    genos.Energy,
                    genos.Hygiene,
                    genos.Bladder,
                    genos.CurrentAction,
                    genos.ActionTimeRemaining,
                    genos.IsPlayerControlled,
                    genos.PlayerId
                ));
            }

            foreach (var company in companyDocs)
            {
                blueprints.Add(new EntityBlueprint(
                    EntityType.Company,
                    (ulong)company.NetworkId,
                    company.Name,
                    0, 0, 0, 0, 0, "Idle", 0, false, null
                ));
            }

            logger?.LogInformation("Restored {GenosCount} genoses and {CompanyCount} companies from Marten",
                genosDocs.Count, companyDocs.Count);

            return (blueprints, genosDocs.Count, companyDocs.Count);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to restore world from Marten; starting with empty world");
            return (blueprints, 0, 0);
        }
    }
}

public record EntityBlueprint(
    EntityType Type,
    ulong NetworkId,
    string Name,
    int Age,
    float Hunger,
    float Energy,
    float Hygiene,
    float Bladder,
    string CurrentAction,
    float ActionTimeRemaining,
    bool IsPlayerControlled,
    string? PlayerId
);

public enum EntityType { Genos, Company }