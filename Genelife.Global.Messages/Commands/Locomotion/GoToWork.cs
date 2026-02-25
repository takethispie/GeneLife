using MassTransit;

namespace Genelife.Life.Messages.Commands.Locomotion;

public record GoToWork(Guid CorrelationId) : CorrelatedBy<Guid>;