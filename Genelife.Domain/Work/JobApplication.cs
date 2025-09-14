namespace Genelife.Domain.Work;

public record JobApplication(
    Guid JobPostingId,
    Guid HumanId,
    DateTime ApplicationDate,
    ApplicationStatus Status,
    float RequestedSalary,
    List<string> Skills,
    int YearsOfExperience,
    float MatchScore = 0.0f
);