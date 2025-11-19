namespace Genelife.Domain.Work;

public record Employee(
    Guid HumanId,
    float Salary,
    DateTime HireDate,
    EmploymentStatus Status,
    float ProductivityScore = 1.0f
);