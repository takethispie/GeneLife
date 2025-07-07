using MassTransit;

namespace Genelife.Domain;

public record Company(
    Guid Id,
    string Name,
    decimal Revenue,
    decimal TaxRate,
    List<Guid> EmployeeIds,
    CompanyType Type,
    int MinEmployees = 5,
    int MaxEmployees = 50
);

public enum CompanyType
{
    Technology,
    Manufacturing,
    Services,
    Retail,
    Healthcare
}