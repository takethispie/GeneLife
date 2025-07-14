using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record JobPostingCreated(Guid CorrelationId, Guid CompanyId, JobPosting JobPosting) : CorrelatedBy<Guid>;