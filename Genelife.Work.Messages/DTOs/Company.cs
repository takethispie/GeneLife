namespace Genelife.Work.Messages.DTOs;

public record Company(
    string Name,
    float Revenue,
    float TaxRate,
    List<Guid> EmployeeIds,
    CompanyType Type,
    float AverageProductivity = 1.0F
);