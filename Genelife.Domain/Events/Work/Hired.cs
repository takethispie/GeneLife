using MassTransit;

namespace Genelife.Domain.Events;

public record Hired(Guid CorrelationId, Guid CompanyId, float PayPerHour) : CorrelatedBy<Guid>;