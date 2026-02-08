using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetJobStatus(Guid CorrelationId, bool Hasjob) : CorrelatedBy<Guid>;