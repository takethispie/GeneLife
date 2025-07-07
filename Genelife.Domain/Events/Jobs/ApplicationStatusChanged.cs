namespace Genelife.Domain.Events.Jobs;

public record ApplicationStatusChanged(
    Guid ApplicationId, 
    Guid JobPostingId, 
    Guid HumanId, 
    ApplicationStatus OldStatus, 
    ApplicationStatus NewStatus,
    string? Reason = null
);