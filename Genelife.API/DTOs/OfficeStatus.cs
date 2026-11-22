namespace Genelife.Api.DTOs;

public sealed class OfficeStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EmployeeCount { get; set; }
    public int OpenSeats { get; set; }
    public int SeatsUsed { get; set; }
    public decimal OperatingCost { get; set; }
    public bool IsClosed { get; set; }
}
