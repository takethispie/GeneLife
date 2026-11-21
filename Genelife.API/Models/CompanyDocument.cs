using Genelife.Api.DTOs;

namespace Genelife.Api.Models;

public class CompanyDocument
{
    public Guid Id { get; set; }
    public ulong NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CompanyStatus ToCompanyStatus()
    {
        return new CompanyStatus
        {
            NetworkId = NetworkId,
            Name = Name,
            EmployeeCount = EmployeeCount
        };
    }
}