namespace Genelife.Domain;

public record JobApplication(
    Guid Id,
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

public enum ApplicationStatus
{
    Submitted,
    UnderReview,
    Interviewing,
    Rejected,
    Accepted,
    Withdrawn,
    Hired
}

public record HumanSkills(
    Guid HumanId,
    List<string> TechnicalSkills,
    List<string> SoftSkills,
    int YearsOfExperience,
    JobLevel CurrentLevel,
    decimal DesiredSalary
);