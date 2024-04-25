using MassTransit;

namespace Genelife.Domain.Commands;

public record GoHome(Guid CorrelationId) : CorrelatedBy<Guid>;