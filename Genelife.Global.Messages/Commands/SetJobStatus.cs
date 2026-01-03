using MassTransit;

namespace Genelife.Global.Messages.Commands;

public record SetJobStatus(Guid CorrelationId, bool Hasjob) : CorrelatedBy<Guid>;