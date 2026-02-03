using MassTransit;

namespace Genelife.Global.Messages.Commands.Locomotion;

public record LeaveWork(Guid CorrelationId) : CorrelatedBy<Guid>;