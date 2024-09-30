using MassTransit;

namespace Genelife.Domain.Events;

public record Resigned(Guid CorrelationId, Guid CompanyId) : CorrelatedBy<Guid>;