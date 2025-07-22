using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record ApplicationStatusChanged(
    Guid CorrelationId, 
    Guid JobPostingId, 
    Guid HumanId, 
    ApplicationStatus OldStatus, 
    ApplicationStatus NewStatus,
    string? Reason = null
) : CorrelatedBy<Guid>;