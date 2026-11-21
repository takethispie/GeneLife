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

    public void Project(EmployeeHired e, IDocumentOperations ops)
    {
        var doc = ops.Query<CompanyDocument>().FirstOrDefault(x => x.NetworkId == e.CompanyId);
        if (doc != null)
        {
            doc.EmployeeCount++;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public void Project(EmployeeFired e, IDocumentOperations ops)
    {
        var doc = ops.Query<CompanyDocument>().FirstOrDefault(x => x.NetworkId == e.CompanyId);
        if (doc != null)
        {
            doc.EmployeeCount--;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public void Project(CompanyDeleted e, IDocumentOperations ops)
    {
        var doc = ops.Query<CompanyDocument>().FirstOrDefault(x => x.NetworkId == e.NetworkId);
        if (doc != null)
            ops.Delete<CompanyDocument>(doc.Id);
    }
}