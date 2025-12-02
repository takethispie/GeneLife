using MassTransit;

namespace Genelife.Work.Messages.Events.Company;

public record PayrollCompleted(Guid CorrelationId, float TotalPaid, float TaxesPaid) : CorrelatedBy<Guid>;