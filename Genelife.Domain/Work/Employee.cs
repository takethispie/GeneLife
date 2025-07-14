namespace Genelife.Domain.Work;

public record Employee(
    Guid HumanId,
    Guid CompanyId,
    decimal Salary,
    DateTime HireDate,
    EmploymentStatus Status,
    decimal ProductivityScore = 1.0m
);