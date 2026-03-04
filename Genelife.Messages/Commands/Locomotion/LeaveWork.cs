using MassTransit;

namespace Genelife.Messages.Commands.Locomotion;

public record LeaveWork(Guid CorrelationId) : CorrelatedBy<Guid>;