namespace Genelife.Domain.Commands.Jobs;

public record SubmitJobApplication(
    Guid JobPostingId,
    Guid HumanId,
    decimal RequestedSalary,
    string CoverLetter,
    List<string> Skills,
    int YearsOfExperience
);