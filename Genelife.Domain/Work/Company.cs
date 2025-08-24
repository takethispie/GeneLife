namespace Genelife.Domain.Work;

public record Company(
    string Name,
    float Revenue,
    float TaxRate,
    List<Guid> EmployeeIds,
    CompanyType Type,
    int MinEmployees = 5,
    int MaxEmployees = 50
);