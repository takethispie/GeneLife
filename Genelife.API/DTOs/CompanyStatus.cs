namespace Genelife.Api.DTOs;

public class CompanyStatus
{
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int OfficeCount { get; set; }
    public decimal Balance { get; set; }
    public decimal ReservedForPayroll { get; set; }
    public decimal Arrears { get; set; }
    public decimal RevenueLastCycle { get; set; }
    public decimal WagesLastCycle { get; set; }
    public string Solvency { get; set; } = string.Empty;
}
