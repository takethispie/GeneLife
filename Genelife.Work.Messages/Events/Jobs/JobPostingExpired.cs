using MassTransit;

namespace Genelife.Work.Messages.Events.Jobs;

public record JobPostingExpired(Guid CorrelationId, Guid jobPostingId) : CorrelatedBy<Guid>;