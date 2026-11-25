using Genelife.Api.DTOs;

namespace Genelife.Api.Models;

public class CompanyDocument
{
    public Guid Id { get; set; }
    public long NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int OfficeCount { get; set; }
    public decimal Balance { get; set; }
    public decimal ReservedForPayroll { get; set; }
    public decimal Arrears { get; set; }
    public decimal RevenueLastCycle { get; set; }
    public decimal WagesLastCycle { get; set; }
    public string Solvency { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CompanyStatus ToCompanyStatus()
    {
        return new CompanyStatus
        {
            NetworkId = (ulong)NetworkId,
            Name = Name,
            EmployeeCount = EmployeeCount,
            OfficeCount = OfficeCount,
            Balance = Balance,
            ReservedForPayroll = ReservedForPayroll,
            Arrears = Arrears,
            RevenueLastCycle = RevenueLastCycle,
            WagesLastCycle = WagesLastCycle,
            Solvency = Solvency
        };
    }
}