namespace Genelife.Api.DTOs;

public class CompanyStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
}
