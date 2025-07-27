using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record ApplicationStatusChanged(
    Guid CorrelationId, 
    Guid JobPostingId,
    Guid HumanId, 
    ApplicationStatus Status
) : CorrelatedBy<Guid>;