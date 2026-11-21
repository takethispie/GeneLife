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
            Id = Guid.NewGuid(),
            NetworkId = e.NetworkId,
            Name = e.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async ValueTask Project(EmployeeHired e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.CompanyId);
        if (doc != null)
        {
            doc.EmployeeCount++;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public async ValueTask Project(EmployeeFired e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.CompanyId);
        if (doc != null)
        {
            doc.EmployeeCount--;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public async ValueTask Project(CompanyDeleted e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.NetworkId);
        if (doc != null)
            ops.Delete<CompanyDocument>(doc.Id);
    }

    private static async ValueTask<CompanyDocument?> FindByNetworkId(IDocumentOperations ops, ulong networkId)
    {
        var docs = await ops.Query<CompanyDocument>().ToListAsync();
        return docs.FirstOrDefault(x => x.NetworkId == networkId);
    }
}