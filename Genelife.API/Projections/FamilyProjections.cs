using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

/// <summary>
/// Family summary per sim. Every handler is an upsert and every collection is a
/// set, so applying the same event twice does not duplicate a fact.
/// </summary>
public sealed partial class FamilyProjection : EventProjection
{
    public async Task Project(SimCreated e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.NetworkId);
        doc.Name = e.Name;
        doc.Age = e.Age;
        doc.Stage = Genelife.Life.LifecycleClassifier.Classify(e.Age).ToString();
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(SimAged e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.SimNetworkId);
        doc.Age = e.Age;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(LifeStageChanged e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.SimNetworkId);
        doc.Stage = e.To;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(SimDied e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.SimNetworkId);
        doc.IsDead = true;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(RelationshipFormed e, IDocumentOperations ops)
    {
        var from = await GetOrCreate(ops, e.FromSimId);
        var to = await GetOrCreate(ops, e.ToSimId);

        switch (e.Kind)
        {
            case "Partner":
                from.PartnerNetworkId = (long)e.ToSimId;
                to.PartnerNetworkId = (long)e.FromSimId;
                break;
            case "ExPartner":
                if (from.PartnerNetworkId == (long)e.ToSimId)
                    from.PartnerNetworkId = 0;
                if (to.PartnerNetworkId == (long)e.FromSimId)
                    to.PartnerNetworkId = 0;
                break;
            case "Parent":
                to.ParentIds.Add((long)e.FromSimId);
                from.ChildIds.Add((long)e.ToSimId);
                break;
            case "Child":
                to.ChildIds.Add((long)e.FromSimId);
                from.ParentIds.Add((long)e.ToSimId);
                break;
            case "Guardian":
                to.GuardianNetworkId = (long)e.FromSimId;
                to.InStateCare = false;
                from.WardIds.Add((long)e.ToSimId);
                break;
            case "Ward":
                from.GuardianNetworkId = (long)e.ToSimId;
                from.InStateCare = false;
                to.WardIds.Add((long)e.FromSimId);
                break;
        }

        from.UpdatedTick = e.Tick;
        to.UpdatedTick = e.Tick;
        from.UpdatedAt = DateTimeOffset.UtcNow;
        to.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(from);
        ops.Store(to);
    }

    public async Task Project(RelationshipEnded e, IDocumentOperations ops)
    {
        var from = await GetOrCreate(ops, e.FromSimId);
        var to = await GetOrCreate(ops, e.ToSimId);

        switch (e.Kind)
        {
            case "Partner":
            case "ExPartner":
                if (from.PartnerNetworkId == (long)e.ToSimId)
                    from.PartnerNetworkId = 0;
                if (to.PartnerNetworkId == (long)e.FromSimId)
                    to.PartnerNetworkId = 0;
                break;
            case "Parent":
                to.ParentIds.Remove((long)e.FromSimId);
                from.ChildIds.Remove((long)e.ToSimId);
                break;
            case "Child":
                to.ChildIds.Remove((long)e.FromSimId);
                from.ParentIds.Remove((long)e.ToSimId);
                break;
            case "Guardian":
                if (to.GuardianNetworkId == (long)e.FromSimId)
                    to.GuardianNetworkId = null;
                from.WardIds.Remove((long)e.ToSimId);
                break;
            case "Ward":
                if (from.GuardianNetworkId == (long)e.ToSimId)
                    from.GuardianNetworkId = null;
                to.WardIds.Remove((long)e.FromSimId);
                break;
        }

        from.UpdatedTick = e.Tick;
        to.UpdatedTick = e.Tick;
        from.UpdatedAt = DateTimeOffset.UtcNow;
        to.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(from);
        ops.Store(to);
    }

    public async Task Project(PartnershipFormed e, IDocumentOperations ops)    {
        var first = await GetOrCreate(ops, e.SimAId);
        var second = await GetOrCreate(ops, e.SimBId);
        first.PartnerNetworkId = (long)e.SimBId;
        second.PartnerNetworkId = (long)e.SimAId;
        first.UpdatedTick = e.Tick;
        second.UpdatedTick = e.Tick;
        ops.Store(first);
        ops.Store(second);
    }

    public async Task Project(PartnershipEnded e, IDocumentOperations ops)
    {
        var first = await GetOrCreate(ops, e.SimAId);
        var second = await GetOrCreate(ops, e.SimBId);
        if (first.PartnerNetworkId == (long)e.SimBId)
            first.PartnerNetworkId = 0;
        if (second.PartnerNetworkId == (long)e.SimAId)
            second.PartnerNetworkId = 0;
        first.UpdatedTick = e.Tick;
        second.UpdatedTick = e.Tick;
        ops.Store(first);
        ops.Store(second);
    }

    public async Task Project(ChildBorn e, IDocumentOperations ops)
    {
        var child = await GetOrCreate(ops, e.ChildNetworkId);
        if (e.ParentANetworkId != 0)
            child.ParentIds.Add((long)e.ParentANetworkId);
        if (e.ParentBNetworkId != 0)
            child.ParentIds.Add((long)e.ParentBNetworkId);
        child.UpdatedTick = e.Tick;
        ops.Store(child);

        if (e.ParentANetworkId != 0)
        {
            var parentA = await GetOrCreate(ops, e.ParentANetworkId);
            parentA.ChildIds.Add((long)e.ChildNetworkId);
            parentA.UpdatedTick = e.Tick;
            ops.Store(parentA);
        }

        if (e.ParentBNetworkId != 0)
        {
            var parentB = await GetOrCreate(ops, e.ParentBNetworkId);
            parentB.ChildIds.Add((long)e.ChildNetworkId);
            parentB.UpdatedTick = e.Tick;
            ops.Store(parentB);
        }
    }

    public async Task Project(BirthRecorded e, IDocumentOperations ops)
    {
        var child = await GetOrCreate(ops, e.ChildNetworkId);
        child.Name = e.Name;
        child.UpdatedTick = e.Tick;
        ops.Store(child);
    }

    public async Task Project(GuardianshipAssigned e, IDocumentOperations ops)
    {
        var child = await GetOrCreate(ops, e.ChildNetworkId);
        if (e.GuardianNetworkId == 0)
        {
            child.InStateCare = true;
            child.GuardianNetworkId = null;
        }
        else
        {
            child.GuardianNetworkId = (long)e.GuardianNetworkId;
            child.InStateCare = false;
            var guardian = await GetOrCreate(ops, e.GuardianNetworkId);
            guardian.WardIds.Add((long)e.ChildNetworkId);
            guardian.UpdatedTick = e.Tick;
            ops.Store(guardian);
        }

        child.UpdatedTick = e.Tick;
        ops.Store(child);
    }

    public async Task Project(StateCareOpened e, IDocumentOperations ops)
    {
        var child = await GetOrCreate(ops, e.ChildNetworkId);
        child.InStateCare = true;
        child.GuardianNetworkId = null;
        child.UpdatedTick = e.Tick;
        ops.Store(child);
    }

    public async Task Project(StateCareClosed e, IDocumentOperations ops)
    {
        var child = await GetOrCreate(ops, e.ChildNetworkId);
        child.InStateCare = false;
        child.GuardianNetworkId = e.GuardianNetworkId == 0 ? null : (long)e.GuardianNetworkId;
        child.UpdatedTick = e.Tick;
        ops.Store(child);
    }

    public async Task Project(TrustOpened e, IDocumentOperations ops)
    {
        var beneficiary = await GetOrCreate(ops, e.BeneficiaryNetworkId);
        beneficiary.TrustIds.Add((long)e.TrustNetworkId);
        beneficiary.UpdatedTick = e.Tick;
        ops.Store(beneficiary);
    }

    public async Task Project(TrustReleased e, IDocumentOperations ops)
    {
        if (e.BeneficiaryNetworkId == 0)
            return;

        var beneficiary = await GetOrCreate(ops, e.BeneficiaryNetworkId);
        beneficiary.TrustIds.Remove((long)e.TrustNetworkId);
        beneficiary.TrustReleasedCents += e.AmountCents;
        beneficiary.UpdatedTick = e.Tick;
        ops.Store(beneficiary);
    }

    public async Task Project(InheritancePaid e, IDocumentOperations ops)
    {
        var heir = await GetOrCreate(ops, e.HeirNetworkId);
        heir.InheritanceCents += e.AmountCents;
        heir.UpdatedTick = e.Tick;
        ops.Store(heir);
    }

    public async Task Project(FamilyAdminActionApplied e, IDocumentOperations ops)
    {
        var existing = await ops.LoadAsync<FamilyAdminActionDocument>(e.CommandId);
        if (existing is not null)
            return;

        ops.Store(new FamilyAdminActionDocument
        {
            Id = e.CommandId,
            CommandId = e.CommandId,
            Action = e.Action,
            IssuedBy = e.IssuedBy,
            Reason = e.Reason,
            SubjectA = (long)e.SubjectA,
            SubjectB = (long)e.SubjectB,
            SubjectC = (long)e.SubjectC,
            Tick = e.Tick
        });
    }

    private static Task<FamilyDocument> GetOrCreate(IDocumentOperations ops, ulong networkId) =>
        ProjectionDocumentStore.GetOrCreate(
            ops,
            DocumentIds.ForNetworkId(networkId),
            document => document.SimNetworkId == (long)networkId,
            () => new FamilyDocument
            {
                Id = DocumentIds.ForNetworkId(networkId),
                SimNetworkId = (long)networkId
            });
}

public sealed partial class RelationshipProjection : EventProjection
{
    public async Task Project(RelationshipFormed e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.RelationshipId);
        doc.FromSimNetworkId = (long)e.FromSimId;
        doc.ToSimNetworkId = (long)e.ToSimId;
        doc.Kind = e.Kind;
        doc.Source = e.Source;
        doc.SinceTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(RelationshipEnded e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.RelationshipId);
        doc.EndedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    private static Task<RelationshipDocument> GetOrCreate(IDocumentOperations ops, ulong networkId) =>
        ProjectionDocumentStore.GetOrCreate(
            ops,
            DocumentIds.ForNetworkId(networkId),
            document => document.RelationshipNetworkId == (long)networkId,
            () => new RelationshipDocument
            {
                Id = DocumentIds.ForNetworkId(networkId),
                RelationshipNetworkId = (long)networkId
            });
}

public sealed partial class PregnancyProjection : EventProjection
{
    public async Task Project(PregnancyStarted e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PregnancyNetworkId);
        doc.CarryingParentNetworkId = (long)e.CarryingParentNetworkId;
        doc.OtherParentNetworkId = (long)e.OtherParentNetworkId;
        doc.ConceivedTick = e.ConceivedTick;
        doc.DueTick = e.DueTick;
        doc.Status = "Active";
        doc.ChildrenExpected = 1;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PregnancyProgressed e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PregnancyNetworkId);
        doc.RiskBasisPoints = e.RiskBasisPoints;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PregnancyLost e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PregnancyNetworkId);
        doc.Status = "Lost";
        doc.ResolvedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PregnancyCancelled e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PregnancyNetworkId);
        doc.Status = "Cancelled";
        doc.ResolvedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(BirthRecorded e, IDocumentOperations ops)
    {
        if (e.PregnancyNetworkId == 0)
            return;

        var doc = await GetOrCreate(ops, e.PregnancyNetworkId);
        doc.Status = "Completed";
        doc.ResolvedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    private static Task<PregnancyDocument> GetOrCreate(IDocumentOperations ops, ulong networkId) =>
        ProjectionDocumentStore.GetOrCreate(
            ops,
            DocumentIds.ForNetworkId(networkId),
            document => document.PregnancyNetworkId == (long)networkId,
            () => new PregnancyDocument
            {
                Id = DocumentIds.ForNetworkId(networkId),
                PregnancyNetworkId = (long)networkId
            });
}

public sealed partial class ChildProjection : EventProjection
{
    public async Task Project(ChildBorn e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.ChildNetworkId);
        doc.ParentANetworkId = e.ParentANetworkId == 0 ? null : (long)e.ParentANetworkId;
        doc.ParentBNetworkId = e.ParentBNetworkId == 0 ? null : (long)e.ParentBNetworkId;
        doc.HouseholdNetworkId = (long)e.HouseholdNetworkId;
        doc.BornTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(BirthRecorded e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.ChildNetworkId);
        doc.Name = e.Name;
        doc.BornTick = doc.BornTick == 0 ? e.Tick : doc.BornTick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(GuardianshipAssigned e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.ChildNetworkId);
        if (e.GuardianNetworkId == 0)
        {
            doc.GuardianNetworkId = null;
            doc.InStateCare = true;
        }
        else
        {
            doc.GuardianNetworkId = (long)e.GuardianNetworkId;
            doc.InStateCare = false;
        }
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(StateCareOpened e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.ChildNetworkId);
        doc.InStateCare = true;
        doc.GuardianNetworkId = null;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(StateCareClosed e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.ChildNetworkId);
        doc.InStateCare = false;
        doc.GuardianNetworkId = e.GuardianNetworkId == 0 ? null : (long)e.GuardianNetworkId;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    private static Task<ChildDocument> GetOrCreate(IDocumentOperations ops, ulong networkId) =>
        ProjectionDocumentStore.GetOrCreate(
            ops,
            DocumentIds.ForNetworkId(networkId),
            document => document.ChildNetworkId == (long)networkId,
            () => new ChildDocument
            {
                Id = DocumentIds.ForNetworkId(networkId),
                ChildNetworkId = (long)networkId
            });
}

/// <summary>
/// Approximate household composition history. Births and merges update the
/// document; the live read model remains authoritative for member-level data.
/// </summary>
public sealed partial class HouseholdCompositionProjection : EventProjection
{
    public async Task Project(ChildBorn e, IDocumentOperations ops)
    {
        if (e.HouseholdNetworkId == 0)
            return;

        var doc = await GetOrCreate(ops, e.HouseholdNetworkId);
        doc.Members++;
        doc.Children++;
        doc.Dependants++;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(HouseholdMerged e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.IntoHouseholdId);
        doc.Source = "merged";
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(HouseholdSplit e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.HouseholdId);
        doc.Source = "split";
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    private static Task<HouseholdCompositionDocument> GetOrCreate(IDocumentOperations ops, ulong networkId) =>
        ProjectionDocumentStore.GetOrCreate(
            ops,
            DocumentIds.ForNetworkId(networkId),
            document => document.HouseholdNetworkId == (long)networkId,
            () => new HouseholdCompositionDocument
            {
                Id = DocumentIds.ForNetworkId(networkId),
                HouseholdNetworkId = (long)networkId
            });
}

public sealed partial class EstateProjection : EventProjection
{
    public async Task Project(EstateSettled e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.SimNetworkId);
        doc.AssetsCents = e.AssetsCents;
        doc.DebtsCents = e.DebtsCents;
        doc.DistributedCents = e.DistributedCents;
        doc.EscheatCents = e.EscheatCents;
        doc.Heirs = e.Heirs;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(InheritancePaid e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.SimNetworkId);
        doc.InheritancePaidCents += e.AmountCents;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(EscheatRecorded e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.SimNetworkId);
        doc.EscheatCents = Math.Max(doc.EscheatCents, e.AmountCents);
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(TrustOpened e, IDocumentOperations ops)
    {
        if (e.SourceSimNetworkId == 0)
            return;

        var doc = await GetOrCreate(ops, e.SourceSimNetworkId);
        doc.TrustsOpenedCents += e.AmountCents;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    private static Task<EstateDocument> GetOrCreate(IDocumentOperations ops, ulong networkId) =>
        ProjectionDocumentStore.GetOrCreate(
            ops,
            DocumentIds.ForNetworkId(networkId),
            document => document.DeceasedNetworkId == (long)networkId,
            () => new EstateDocument
            {
                Id = DocumentIds.ForNetworkId(networkId),
                DeceasedNetworkId = (long)networkId
            });
}
