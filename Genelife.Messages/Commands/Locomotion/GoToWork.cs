using MassTransit;

namespace Genelife.Messages.Commands.Locomotion;

public record GoToWork(Guid CorrelationId) : CorrelatedBy<Guid>;