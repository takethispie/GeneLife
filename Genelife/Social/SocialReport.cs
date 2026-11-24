namespace Genelife.Social;

public enum ReportReason : byte { Harassment, Spam, Hate, Impersonation, Other }

public enum ReportStatus : byte { Open, InReview, Resolved, Dismissed }

/// <summary>
/// A moderation report. Evidence is a snapshot, never a live reference, so it
/// survives deletion of the original content.
/// </summary>
public sealed record SocialReport
{
    public Guid Id { get; init; }
    public string ReporterPlayerId { get; init; } = string.Empty;
    public string TargetPlayerId { get; init; } = string.Empty;
    public ReportReason Reason { get; init; }
    public Guid? MessageId { get; init; }
    public string? Evidence { get; init; }
    public long CreatedTick { get; init; }
    public ReportStatus Status { get; init; }
}

public sealed record SocialGraphSnapshot(
    IReadOnlyList<SocialEdge> Edges,
    IReadOnlyList<SocialRequest> Requests,
    IReadOnlyList<SocialReport> Reports);

public sealed record SocialOverview(
    string PlayerId,
    IReadOnlyList<SocialEdge> Friends,
    IReadOnlyList<SocialEdge> Blocks,
    IReadOnlyList<SocialEdge> Mutes,
    IReadOnlyList<SocialRequest> IncomingRequests,
    IReadOnlyList<SocialRequest> OutgoingRequests)
{
    public static SocialOverview Empty(string playerId) => new(
        playerId,
        Array.Empty<SocialEdge>(),
        Array.Empty<SocialEdge>(),
        Array.Empty<SocialEdge>(),
        Array.Empty<SocialRequest>(),
        Array.Empty<SocialRequest>());
}

public sealed record SocialEdgeResult(bool Accepted, string? Code)
{
    public static SocialEdgeResult Ok() => new(true, null);
    public static SocialEdgeResult Reject(string code) => new(false, code);
}

public sealed record SocialRequestResult(bool Accepted, string? Code, SocialRequest? Request)
{
    public static SocialRequestResult Reject(string code) => new(false, code, null);
}

public sealed record SocialReportResult(bool Accepted, string? Code, string? Notice, SocialReport? Report)
{
    public static SocialReportResult Reject(string code, string notice) => new(false, code, notice, null);
}
