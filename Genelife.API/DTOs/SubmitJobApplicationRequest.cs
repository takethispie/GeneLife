namespace Genelife.API.DTOs;

public record SubmitJobApplicationRequest(
    Guid JobPostingId,
    Guid HumanId,
    List<string> Skills,
    int Experience,
    decimal RequestedSalary,
    string? CoverLetter = null
);