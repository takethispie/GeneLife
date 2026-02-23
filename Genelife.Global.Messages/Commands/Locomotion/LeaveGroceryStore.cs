using MassTransit;

namespace Genelife.Global.Messages.Commands.Locomotion;

public record LeaveGroceryStore(
    Guid CorrelationId,
    Guid HumanId
) : CorrelatedBy<Guid>;