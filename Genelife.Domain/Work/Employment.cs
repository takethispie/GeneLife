namespace Genelife.Domain.Work;

public record Employment(
    List<string> Skills,
    int YearsOfExperience,
    Guid? CurrentEmployerId = null,
    decimal? CurrentSalary = null,
    EmploymentStatus EmploymentStatus = EmploymentStatus.Unemployed,
    DateTime? LastJobSearchDate = null,
    bool IsActivelyJobSeeking = true
)
{
    public List<string> Skills { get; init; } = Skills ?? new List<string>();
}