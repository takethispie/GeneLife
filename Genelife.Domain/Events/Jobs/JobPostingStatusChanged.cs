using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record JobPostingStatusChanged(
    Guid CorrelationId, 
    Guid CompanyId, 
    JobPostingStatus OldStatus, 
    JobPostingStatus NewStatus,
    string? Reason = null
) : CorrelatedBy<Guid>;