using MassTransit;

namespace Genelife.Messages.Commands;

public record SetJobStatus(Guid CorrelationId, bool Hasjob) : CorrelatedBy<Guid>;