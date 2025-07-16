using MassTransit;

namespace Genelife.Domain.Commands.Cheat;

public record SetActivelySeekingJob(Guid CorrelationId, bool Value) : CorrelatedBy<Guid>;