using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetJobStatus(Guid CorrelationId, bool Hasjob) : CorrelatedBy<Guid>;