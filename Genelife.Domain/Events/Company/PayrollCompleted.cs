using MassTransit;

namespace Genelife.Domain.Events.Company;

public record PayrollCompleted(Guid CorrelationId, decimal TotalPaid, decimal TaxesPaid) : CorrelatedBy<Guid>;