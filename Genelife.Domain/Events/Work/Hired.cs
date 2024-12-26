using MassTransit;

namespace Genelife.Domain.Events.Work;

public record Hired(Guid CorrelationId, Guid CompanyId, float PayPerHour) : CorrelatedBy<Guid>;