using MassTransit;

namespace Genelife.Messages.Events.Company;

public record PayrollCompleted(Guid CorrelationId, float TotalPaid, float TaxesPaid) : CorrelatedBy<Guid>;