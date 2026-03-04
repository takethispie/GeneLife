using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record SetActivelySeekingJob(Guid CorrelationId, bool Value) : CorrelatedBy<Guid>;