namespace Genelife.Domain.Work;

public record JobApplication(
    Guid JobPostingId,
    Guid HumanId,
    DateTime ApplicationDate,
    ApplicationStatus Status,
    decimal RequestedSalary,
    string CoverLetter,
    List<string> Skills,
    int YearsOfExperience,
    decimal MatchScore = 0.0m
);