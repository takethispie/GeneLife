using MassTransit;

namespace Genelife.Domain.Events.Living;

public record CreateHuman(Guid CorrelationId, Human Human) : CorrelatedBy<Guid>;