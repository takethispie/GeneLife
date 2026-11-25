using Genelife.Api.DTOs;

namespace Genelife.Api.Services;

/// <summary>
/// Read-model port for sim and company status documents. A null result means
/// the backing store was unavailable, so callers can fall back to live state.
/// </summary>
public interface IReadModelStore
{
    Task<IReadOnlyList<SimStatus>?> TryGetAllSimsAsync(CancellationToken cancellationToken = default);

    Task<CompanyStatus?> TryGetCompanyAsync(ulong networkId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompanyStatus>?> TryGetAllCompaniesAsync(CancellationToken cancellationToken = default);
}
