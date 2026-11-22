namespace Genelife.Api.DTOs;

public sealed class EmployeeStatus
{
    public ulong SimNetworkId { get; set; }
    public string Sim { get; set; } = string.Empty;
    public ulong CompanyNetworkId { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}
