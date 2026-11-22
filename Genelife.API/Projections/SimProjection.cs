using Genelife.Api.Models;
using Genelife.Life;
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
            Id = DocumentIds.ForNetworkId(e.NetworkId),
            NetworkId = (long)e.NetworkId,
            Name = e.Name,
            Age = e.Age,
            Stage = LifecycleClassifier.Classify(e.Age).ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async ValueTask Project(SimActionStarted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.CurrentAction = e.ActionType;
            doc.ActionTimeRemaining = e.Duration;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(SimActionCompleted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.CurrentAction = "Idle";
            doc.ActionTimeRemaining = 0f;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(SimNeedsChanged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.Hunger = e.Hunger;
            doc.Energy = e.Energy;
            doc.Hygiene = e.Hygiene;
            doc.Bladder = e.Bladder;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(SimAged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Age = e.Age;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(LifeStageChanged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Stage = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(HealthDeclined e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Health = e.Health;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(CareNeeded e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Health = e.Health;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(StudentGraduated e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Stage = "Work";
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(PromotionGranted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Seniority = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(DemotionApplied e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Seniority = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async ValueTask Project(SimDeleted e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.NetworkId);
        if (doc != null)
            ops.Delete<SimDocument>(doc.Id);
    }

    private static Task<SimDocument?> FindByNetworkId(IDocumentOperations ops, ulong networkId)
    {
        return ops.Query<SimDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
    }

    private static async ValueTask UpdateByNetworkId(IDocumentOperations ops, ulong networkId, Action<SimDocument> update)
    {
        var doc = await FindByNetworkId(ops, networkId);
        if (doc != null)
        {
            update(doc);
            ops.Store(doc);
        }
    }
}