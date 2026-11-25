namespace Genelife.Api.DTOs;

public sealed class EmployeeStatus
{
    public ulong GenosNetworkId { get; set; }
    public string Genos { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}
