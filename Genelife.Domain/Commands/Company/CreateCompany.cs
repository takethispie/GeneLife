using MassTransit;

namespace Genelife.Domain.Commands.Company;

public record CreateCompany(Guid CorrelationId, Domain.Company Company) : CorrelatedBy<Guid>;