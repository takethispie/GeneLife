using Genelife.Api.Models;
using Genelife.Social;
using Marten;

namespace Genelife.Api.Social;

/// <summary>
/// Marten-backed report and moderation audit storage. Reports and actions are
/// deliberately separate from the world snapshot and share a longer retention
/// window than chat.
/// </summary>
public sealed class MartenSocialAuditStore : ISocialAuditStore
{
    private readonly IDocumentStore store;

    public MartenSocialAuditStore(IDocumentStore store)
    {
        this.store = store;
    }

    public IReadOnlyList<SocialReport> LoadReports()
    {
        using var session = store.QuerySession();
        return session.Query<SocialReportDocument>()
            .OrderBy(document => document.CreatedTick)
            .ToListAsync()
            .GetAwaiter()
            .GetResult()
            .Select(ToReport)
            .ToArray();
    }

    public void UpsertReport(SocialReport report)
    {
        using var session = store.LightweightSession();
        session.Store(ToDocument(report));
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public bool SetReportStatus(Guid reportId, ReportStatus status)
    {
        using var session = store.LightweightSession();
        var document = session.LoadAsync<SocialReportDocument>(reportId).GetAwaiter().GetResult();
        if (document is null)
            return false;

        document.Status = (byte)status;
        session.Store(document);
        session.SaveChangesAsync().GetAwaiter().GetResult();
        return true;
    }

    public int TrimReportsBefore(long tick)
    {
        using var session = store.LightweightSession();
        var documents = session.Query<SocialReportDocument>()
            .Where(document => document.CreatedTick < tick)
            .ToListAsync()
            .GetAwaiter()
            .GetResult();
        if (documents.Count == 0)
            return 0;

        session.DeleteObjects(documents);
        session.SaveChangesAsync().GetAwaiter().GetResult();
        return documents.Count;
    }

    public IReadOnlyList<ModerationAction> LoadActions()
    {
        using var session = store.QuerySession();
        return session.Query<ModerationActionDocument>()
            .OrderBy(document => document.CreatedTick)
            .ToListAsync()
            .GetAwaiter()
            .GetResult()
            .Select(ToAction)
            .ToArray();
    }

    public void UpsertAction(ModerationAction action)
    {
        using var session = store.LightweightSession();
        session.Store(ToDocument(action));
        session.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public int TrimActionsBefore(long tick)
    {
        using var session = store.LightweightSession();
        var documents = session.Query<ModerationActionDocument>()
            .Where(document => document.CreatedTick < tick)
            .ToListAsync()
            .GetAwaiter()
            .GetResult();
        if (documents.Count == 0)
            return 0;

        session.DeleteObjects(documents);
        session.SaveChangesAsync().GetAwaiter().GetResult();
        return documents.Count;
    }

    private static SocialReportDocument ToDocument(SocialReport report) => new()
    {
        Id = report.Id,
        ReporterPlayerId = report.ReporterPlayerId,
        TargetPlayerId = report.TargetPlayerId,
        Reason = (byte)report.Reason,
        MessageId = report.MessageId,
        Evidence = report.Evidence,
        CreatedTick = report.CreatedTick,
        Status = (byte)report.Status
    };

    private static SocialReport ToReport(SocialReportDocument document) => new()
    {
        Id = document.Id,
        ReporterPlayerId = document.ReporterPlayerId,
        TargetPlayerId = document.TargetPlayerId,
        Reason = (ReportReason)document.Reason,
        MessageId = document.MessageId,
        Evidence = document.Evidence,
        CreatedTick = document.CreatedTick,
        Status = (ReportStatus)document.Status
    };

    private static ModerationActionDocument ToDocument(ModerationAction action) => new()
    {
        Id = action.Id,
        ReportId = action.ReportId,
        ReversesActionId = action.ReversesActionId,
        ModeratorId = action.ModeratorId,
        TargetPlayerId = action.TargetPlayerId,
        CounterpartyPlayerId = action.CounterpartyPlayerId,
        Kind = (byte)action.Kind,
        DurationTicks = action.DurationTicks,
        CreatedTick = action.CreatedTick,
        EndsTick = action.EndsTick,
        Reason = action.Reason,
        Evidence = action.Evidence,
        MessageId = action.MessageId,
        VenueNetworkId = action.VenueNetworkId
    };

    private static ModerationAction ToAction(ModerationActionDocument document) => new()
    {
        Id = document.Id,
        ReportId = document.ReportId,
        ReversesActionId = document.ReversesActionId,
        ModeratorId = document.ModeratorId,
        TargetPlayerId = document.TargetPlayerId,
        CounterpartyPlayerId = document.CounterpartyPlayerId,
        Kind = (ModerationActionKind)document.Kind,
        DurationTicks = document.DurationTicks,
        CreatedTick = document.CreatedTick,
        EndsTick = document.EndsTick,
        Reason = document.Reason,
        Evidence = document.Evidence,
        MessageId = document.MessageId,
        VenueNetworkId = document.VenueNetworkId
    };
}
