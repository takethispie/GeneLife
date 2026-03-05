using MassTransit;

namespace Genelife.Messages.Events.Jobs;

public record JobPostingExpired(Guid CorrelationId, Guid jobPostingId) : CorrelatedBy<Guid>;