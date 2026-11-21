using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

public partial class SimProjection : EventProjection
{
    public SimDocument Create(SimCreated e)
    {
        return new SimDocument
        {
            Id = Guid.NewGuid(),
            NetworkId = e.NetworkId,
            Name = e.Name,
            Age = e.Age,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Project(SimActionStarted e, IDocumentOperations ops)
    {
        var doc = ops.Query<SimDocument>().FirstOrDefault(x => x.NetworkId == e.NetworkId);
        if (doc != null)
        {
            doc.CurrentAction = e.ActionType;
            doc.ActionTimeRemaining = e.Duration;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public void Project(SimActionCompleted e, IDocumentOperations ops)
    {
        var doc = ops.Query<SimDocument>().FirstOrDefault(x => x.NetworkId == e.NetworkId);
        if (doc != null)
        {
            doc.CurrentAction = "Idle";
            doc.ActionTimeRemaining = 0f;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public void Project(SimNeedsChanged e, IDocumentOperations ops)
    {
        var doc = ops.Query<SimDocument>().FirstOrDefault(x => x.NetworkId == e.NetworkId);
        if (doc != null)
        {
            doc.Hunger = e.Hunger;
            doc.Energy = e.Energy;
            doc.Hygiene = e.Hygiene;
            doc.Bladder = e.Bladder;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            ops.Store(doc);
        }
    }

    public void Project(SimDeleted e, IDocumentOperations ops)
    {
        var doc = ops.Query<SimDocument>().FirstOrDefault(x => x.NetworkId == e.NetworkId);
        if (doc != null)
            ops.Delete<SimDocument>(doc.Id);
    }
}