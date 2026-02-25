using MassTransit;

namespace Genelife.Life.Messages.Commands.Locomotion;

public record LeaveWork(Guid CorrelationId) : CorrelatedBy<Guid>;