namespace Genelife.Domain.Events.Jobs;

public record JobPostingStatusChanged(
    Guid JobPostingId, 
    Guid CompanyId, 
    JobPostingStatus OldStatus, 
    JobPostingStatus NewStatus,
    string? Reason = null
);