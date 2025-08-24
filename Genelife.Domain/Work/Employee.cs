namespace Genelife.Domain.Work;

public record Employee(
    Guid HumanId,
    Guid CompanyId,
    float Salary,
    DateTime HireDate,
    EmploymentStatus Status,
    float ProductivityScore = 1.0f
);