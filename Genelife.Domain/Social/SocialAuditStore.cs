namespace Genelife.Social;

/// <summary>
/// Audit persistence for reports and moderation actions. The Marten
/// implementation keeps these records out of the world snapshot; the in-memory
/// implementation is used for tests and local development without Marten.
/// </summary>
public interface ISocialAuditStore
{
    IReadOnlyList<SocialReport> LoadReports();

    void UpsertReport(SocialReport report);

    bool SetReportStatus(Guid reportId, ReportStatus status);

    int TrimReportsBefore(long tick);

    IReadOnlyList<ModerationAction> LoadActions();

    void UpsertAction(ModerationAction action);

    int TrimActionsBefore(long tick);
}

public sealed class InMemorySocialAuditStore : ISocialAuditStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, SocialReport> reports = new();
    private readonly Dictionary<Guid, ModerationAction> actions = new();

    public IReadOnlyList<SocialReport> LoadReports()
    {
        lock (gate)
            return reports.Values.OrderBy(report => report.CreatedTick).ToArray();
    }

    public void UpsertReport(SocialReport report)
    {
        lock (gate)
            reports[report.Id] = report;
    }

    public bool SetReportStatus(Guid reportId, ReportStatus status)
    {
        lock (gate)
        {
            if (!reports.TryGetValue(reportId, out var report))
                return false;
            reports[reportId] = report with { Status = status };
            return true;
        }
    }

    public int TrimReportsBefore(long tick)
    {
        lock (gate)
        {
            var ids = reports.Values
                .Where(report => report.CreatedTick < tick)
                .Select(report => report.Id)
                .ToArray();
            foreach (var id in ids)
                reports.Remove(id);
            return ids.Length;
        }
    }

    public IReadOnlyList<ModerationAction> LoadActions()
    {
        lock (gate)
            return actions.Values.OrderBy(action => action.CreatedTick).ToArray();
    }

    public void UpsertAction(ModerationAction action)
    {
        lock (gate)
            actions[action.Id] = action;
    }

    public int TrimActionsBefore(long tick)
    {
        lock (gate)
        {
            var ids = actions.Values
                .Where(action => action.CreatedTick < tick)
                .Select(action => action.Id)
                .ToArray();
            foreach (var id in ids)
                actions.Remove(id);
            return ids.Length;
        }
    }
}
