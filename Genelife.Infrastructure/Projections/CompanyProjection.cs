using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

public partial class CompanyProjection : EventProjection
{
    public CompanyDocument Create(CompanyCreated e)
    {
        return new CompanyDocument
        {
            Id = DocumentIds.ForNetworkId(e.NetworkId),
            NetworkId = (long)e.NetworkId,
            Name = e.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
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