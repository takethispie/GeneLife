using Arch.Core;
using Genelife.Api.Models;
using Genelife.Components.Company;
using Genelife.Components.Employment;
using Genelife.Components.Gen;
using Genelife.Enums;
using Marten;

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

    public (List<EntityBlueprint> entities, int simCount, int companyCount) Restore(
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
            using var session = documentStore.LightweightSession();

            var simDocs = session.Query<SimDocument>().ToList();
            var companyDocs = session.Query<CompanyDocument>().ToList();

            foreach (var sim in simDocs)
            {
                blueprints.Add(new EntityBlueprint(
                    EntityType.Sim,
                    sim.NetworkId,
                    sim.Name,
                    sim.Age,
                    sim.Hunger,
                    sim.Energy,
                    sim.Hygiene,
                    sim.Bladder,
                    sim.CurrentAction,
                    sim.ActionTimeRemaining,
                    sim.IsPlayerControlled,
                    sim.PlayerId
                ));
            }

            foreach (var company in companyDocs)
            {
                blueprints.Add(new EntityBlueprint(
                    EntityType.Company,
                    company.NetworkId,
                    company.Name,
                    0, 0, 0, 0, 0, "Idle", 0, false, null
                ));
            }

            logger?.LogInformation("Restored {SimCount} sims and {CompanyCount} companies from Marten",
                simDocs.Count, companyDocs.Count);

            return (blueprints, simDocs.Count, companyDocs.Count);
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

public enum EntityType { Sim, Company }