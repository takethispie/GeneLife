using MassTransit;

namespace Genelife.Work.Messages.Commands.Company;

public record CreateCompany(Guid CorrelationId, DTOs.Company Company, Guid MainOfficeId) : CorrelatedBy<Guid>;