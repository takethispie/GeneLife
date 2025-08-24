using MassTransit;

namespace Genelife.Domain.Events.Company;

public record SalaryPaid(Guid CorrelationId, float Amount, float TaxDeducted) : CorrelatedBy<Guid>;