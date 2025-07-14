using MassTransit;

namespace Genelife.Domain.Events.Company;

public record CompanyCreated(Guid CorrelationId, Domain.Company Company) : CorrelatedBy<Guid>;