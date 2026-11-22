namespace Genelife.Api.DTOs;

public sealed class JobPostingStatus
{
    public ulong NetworkId { get; set; }
    public string Position { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public string Requirements { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
    public int TotalSeats { get; set; }
    public long PostedAtTick { get; set; }
    public bool IsClosed { get; set; }
}
