using MassTransit;

namespace Genelife.Work.Messages.Commands.Jobs;

public record SetActivelySeekingJob(Guid CorrelationId, bool Value) : CorrelatedBy<Guid>;