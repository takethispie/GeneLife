using MassTransit;

namespace Genelife.Messages.Commands.Locomotion;

public record GoToGroceryStore(
    Guid CorrelationId,
    Guid HumanId
) : CorrelatedBy<Guid>;
