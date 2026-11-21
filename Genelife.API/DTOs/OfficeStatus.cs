namespace Genelife.Api.DTOs;

public sealed class OfficeStatus
{
    public string Name { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int EmployeeCount { get; set; }
}
