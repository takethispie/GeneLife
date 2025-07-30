using MassTransit;

namespace Genelife.Domain.Events.Company;

public record SalaryPaid(Guid CorrelationId, decimal Amount, decimal TaxDeducted) : CorrelatedBy<Guid>;