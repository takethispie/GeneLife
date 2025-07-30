using MassTransit;

namespace Genelife.Domain.Commands.Company;

public record CreateCompany(Guid CorrelationId, Work.Company Company) : CorrelatedBy<Guid>;