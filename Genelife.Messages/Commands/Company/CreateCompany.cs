using MassTransit;

namespace Genelife.Messages.Commands.Company;

public record CreateCompany(Guid CorrelationId, Domain.Company Company, float X, float Y, float Z) : CorrelatedBy<Guid>;