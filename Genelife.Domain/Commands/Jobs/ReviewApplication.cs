namespace Genelife.Domain.Commands.Jobs;

public record ReviewApplication(
    Guid ApplicationId,
    Guid JobPostingId,
    ApplicationStatus NewStatus,
    string? ReviewNotes = null,
    decimal? OfferedSalary = null
);