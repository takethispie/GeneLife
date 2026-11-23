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

    public async Task Project(SimActionStarted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.CurrentAction = e.ActionType;
            doc.ActionTimeRemaining = e.Duration;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(SimActionCompleted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.CurrentAction = "Idle";
            doc.ActionTimeRemaining = 0f;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(SimNeedsChanged e, IDocumentOperations ops)
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

    public async Task Project(SimAged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Age = e.Age;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(LifeStageChanged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Stage = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(HealthDeclined e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Health = e.Health;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(CareNeeded e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Health = e.Health;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(StudentGraduated e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Stage = "Work";
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(PromotionGranted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Seniority = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(DemotionApplied e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.Seniority = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(AppearanceMigrated e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.SkinTone = (byte)e.SkinTone;
            doc.FaceShape = (byte)e.FaceShape;
            doc.AppearanceVersion = e.AppearanceVersion;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(AppearanceVersionChanged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.SimNetworkId, doc =>
        {
            doc.AppearanceVersion = e.ToVersion;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(SimDeleted e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.NetworkId);
        if (doc != null)
            ops.Delete<SimDocument>(doc.Id);
    }

    private static async Task<SimDocument?> FindByNetworkId(IDocumentOperations ops, ulong networkId)
    {
        var documentId = DocumentIds.ForNetworkId(networkId);
        var pending = await ops.LoadAsync<SimDocument>(documentId);
        if (pending is not null)
            return pending;

        return await ops.Query<SimDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
    }

    private static async Task UpdateByNetworkId(IDocumentOperations ops, ulong networkId, Action<SimDocument> update)
    {
        var doc = await FindByNetworkId(ops, networkId);
        if (doc != null)
        {
            update(doc);
            ops.Store(doc);
        }
    }
}