namespace Genelife.Api.Models;

public sealed class SocialReportDocument
{
    public Guid Id { get; set; }
    public string ReporterPlayerId { get; set; } = "";
    public string TargetPlayerId { get; set; } = "";
    public byte Reason { get; set; }
    public Guid? MessageId { get; set; }
    public string? Evidence { get; set; }
    public long CreatedTick { get; set; }
    public byte Status { get; set; }
}

public sealed class ModerationActionDocument
{
    public Guid Id { get; set; }
    public Guid? ReportId { get; set; }
    public Guid? ReversesActionId { get; set; }
    public string ModeratorId { get; set; } = "";
    public string TargetPlayerId { get; set; } = "";
    public string? CounterpartyPlayerId { get; set; }
    public byte Kind { get; set; }
    public long DurationTicks { get; set; }
    public long CreatedTick { get; set; }
    public long? EndsTick { get; set; }
    public string Reason { get; set; } = "";
    public string? Evidence { get; set; }
    public Guid? MessageId { get; set; }
    public ulong? VenueNetworkId { get; set; }
}
