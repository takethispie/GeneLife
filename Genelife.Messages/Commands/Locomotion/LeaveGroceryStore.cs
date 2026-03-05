using MassTransit;

namespace Genelife.Messages.Commands.Locomotion;

public record LeaveGroceryStore(
    Guid CorrelationId,
    Guid HumanId
) : CorrelatedBy<Guid>;