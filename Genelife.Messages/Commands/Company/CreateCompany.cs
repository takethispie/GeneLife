using MassTransit;

namespace Genelife.Messages.Commands.Company;

public record CreateCompany(Guid CorrelationId, Domain.Work.Company Company, float X, float Y, float Z) : CorrelatedBy<Guid>;