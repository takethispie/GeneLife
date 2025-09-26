using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record JobPostingExpired(Guid CorrelationId, Guid CompanyId) : CorrelatedBy<Guid>;