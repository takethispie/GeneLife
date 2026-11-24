namespace Genelife.Api.Models;

/// <summary>
/// Player-life audit document. It is a rebuildable projection over player
/// events; the authoritative state remains in the world snapshot.
/// </summary>
public sealed class PlayerDocument
{
    public string Id { get; set; } = string.Empty;
    public ulong SimNetworkId { get; set; }
    public string ContinuationPolicy { get; set; } = "Respawn";
    public bool AwaitingContinuation { get; set; }
    public bool Observer { get; set; }
    public bool Employed { get; set; }
    public string Position { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public ulong OfficeNetworkId { get; set; }
    public long SalaryCents { get; set; }
    public bool Housed { get; set; }
    public ulong HouseholdNetworkId { get; set; }
    public ulong UnitNetworkId { get; set; }
    public long RentCents { get; set; }
    public long CommandsApplied { get; set; }
    public Guid? LastCommandId { get; set; }
    public string? LastCommandKind { get; set; }
    public string? LastCommandStatus { get; set; }
    public string? LastCommandCode { get; set; }
    public long LastCommandTick { get; set; }
    public long UpdatedTick { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
