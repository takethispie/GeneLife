using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

public partial class CompanyProjection : EventProjection
{
    public async Task Project(CompanyCreated e, IDocumentOperations ops)
    {
        var id = DocumentIds.ForNetworkId(e.NetworkId);
        var doc = await ops.LoadAsync<CompanyDocument>(id)
            ?? new CompanyDocument
            {
                Id = id,
                NetworkId = (long)e.NetworkId,
                CreatedAt = DateTimeOffset.UtcNow
            };

        doc.Name = e.Name;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(EmployeeHired e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.CompanyId);
        if (doc != null)
        {
            doc.EmployeeCount++;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public async Task Project(EmployeeFired e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.CompanyId);
        if (doc != null)
        {
            doc.EmployeeCount--;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public async Task Project(CompanyDeleted e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.NetworkId);
        if (doc != null)
            ops.Delete<CompanyDocument>(doc.Id);
    }

    private static Task<CompanyDocument?> FindByNetworkId(IDocumentOperations ops, ulong networkId)
    {
        return ops.Query<CompanyDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
    }
}