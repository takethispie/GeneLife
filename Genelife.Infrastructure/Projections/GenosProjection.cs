using Genelife.Api.Models;
using Genelife.Life;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

public partial class GenosProjection : EventProjection
{
    public GenosDocument Create(GenosCreated e)
    {
        return new GenosDocument
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

    public async Task Project(GenosActionStarted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.CurrentAction = e.ActionType;
            doc.ActionTimeRemaining = e.Duration;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(GenosActionCompleted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.NetworkId, doc =>
        {
            doc.CurrentAction = "Idle";
            doc.ActionTimeRemaining = 0f;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(GenosNeedsChanged e, IDocumentOperations ops)
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

    public async Task Project(GenosAged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Age = e.Age;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(LifeStageChanged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Stage = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(HealthDeclined e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Health = e.Health;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(CareNeeded e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Health = e.Health;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(StudentGraduated e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Stage = "Work";
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(PromotionGranted e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Seniority = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(DemotionApplied e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.Seniority = e.To;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(AppearanceMigrated e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.SkinTone = (byte)e.SkinTone;
            doc.FaceShape = (byte)e.FaceShape;
            doc.AppearanceVersion = e.AppearanceVersion;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(AppearanceVersionChanged e, IDocumentOperations ops)
    {
        await UpdateByNetworkId(ops, e.GenosNetworkId, doc =>
        {
            doc.AppearanceVersion = e.ToVersion;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
        });
    }

    public async Task Project(GenosDeleted e, IDocumentOperations ops)
    {
        var doc = await FindByNetworkId(ops, e.NetworkId);
        if (doc != null)
            ops.Delete<GenosDocument>(doc.Id);
    }

    private static async Task<GenosDocument?> FindByNetworkId(IDocumentOperations ops, ulong networkId)
    {
        var documentId = DocumentIds.ForNetworkId(networkId);
        var pending = await ops.LoadAsync<GenosDocument>(documentId);
        if (pending is not null)
            return pending;

        return await ops.Query<GenosDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
    }

    private static async Task UpdateByNetworkId(IDocumentOperations ops, ulong networkId, Action<GenosDocument> update)
    {
        var doc = await FindByNetworkId(ops, networkId);
        if (doc != null)
        {
            update(doc);
            ops.Store(doc);
        }
    }
}