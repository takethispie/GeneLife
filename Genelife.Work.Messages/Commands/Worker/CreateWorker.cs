using MassTransit;

namespace Genelife.Work.Messages.Commands.Worker;

public record CreateWorker(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;