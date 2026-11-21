namespace Genelife.Api.DTOs;

public sealed class JobPostingStatus
{
    public string Position { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Office { get; set; } = string.Empty;
    public float Salary { get; set; }
    public string Requirements { get; set; } = string.Empty;
    public int OpenPositions { get; set; }
}
