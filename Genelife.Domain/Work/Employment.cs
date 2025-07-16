namespace Genelife.Domain.Work;

public record Employment(
    List<string> Skills,
    int YearsOfExperience,
    Guid CurrentEmployerId,
    decimal? CurrentSalary = null,
    EmploymentStatus Status = EmploymentStatus.Unemployed,
    DateTime? LastJobSearchDate = null,
    bool IsActivelyJobSeeking = true
);