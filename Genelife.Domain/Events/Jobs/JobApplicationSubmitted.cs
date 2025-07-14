using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record JobApplicationSubmitted(Guid CorrelationId, Guid JobPostingId, Guid HumanId, JobApplication Application) : CorrelatedBy<Guid>;