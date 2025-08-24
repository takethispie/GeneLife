using MassTransit;

namespace Genelife.Domain.Events.Company;

public record PayrollCompleted(Guid CorrelationId, float TotalPaid, float TaxesPaid) : CorrelatedBy<Guid>;