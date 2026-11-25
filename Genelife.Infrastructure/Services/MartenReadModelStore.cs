using Genelife.Api.DTOs;
using Genelife.Api.Models;
using Marten;
using Microsoft.Extensions.Logging;

namespace Genelife.Api.Services;

/// <summary>
/// Marten-backed implementation of the sim/company read model. Returns null when
/// the document store is missing or a query fails, so the caller can fall back
/// to live simulation state.
/// </summary>
public sealed class MartenReadModelStore : IReadModelStore
{
    private readonly IDocumentStore? documentStore;
    private readonly ILogger<MartenReadModelStore>? logger;

    public MartenReadModelStore(IDocumentStore? documentStore = null, ILogger<MartenReadModelStore>? logger = null)
    {
        this.documentStore = documentStore;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<SimStatus>?> TryGetAllSimsAsync(CancellationToken cancellationToken = default)
    {
        if (documentStore == null)
            return null;

        try
        {
            await using var session = documentStore.LightweightSession();
            var docs = await session.Query<SimDocument>().ToListAsync(cancellationToken);
            return docs
                .GroupBy(d => d.NetworkId)
                .Select(group => group.MaxBy(d => d.UpdatedAt)!)
                .Select(d => d.ToSimStatus())
                .OrderBy(sim => sim.NetworkId)
                .ToList();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Marten query for GetAllSims failed; falling back to snapshot");
            return null;
        }
    }

    public async Task<CompanyStatus?> TryGetCompanyAsync(ulong networkId, CancellationToken cancellationToken = default)
    {
        if (documentStore == null)
            return null;

        try
        {
            await using var session = documentStore.LightweightSession();
            var doc = await session.Query<CompanyDocument>()
                .OrderByDescending(d => d.UpdatedAt)
                .FirstOrDefaultAsync(d => d.NetworkId == (long)networkId, cancellationToken);
            return doc?.ToCompanyStatus();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Marten query for GetCompany failed; falling back to snapshot");
            return null;
        }
    }

    public async Task<IReadOnlyList<CompanyStatus>?> TryGetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        if (documentStore == null)
            return null;

        try
        {
            await using var session = documentStore.LightweightSession();
            var docs = await session.Query<CompanyDocument>().ToListAsync(cancellationToken);
            return docs
                .GroupBy(d => d.NetworkId)
                .Select(group => group.MaxBy(d => d.UpdatedAt)!)
                .Select(d => d.ToCompanyStatus())
                .OrderBy(company => company.NetworkId)
                .ToList();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Marten query for GetAllCompanies failed; falling back to snapshot");
            return null;
        }
    }
}
