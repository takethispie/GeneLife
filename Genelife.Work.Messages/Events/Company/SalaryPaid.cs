using MassTransit;

namespace Genelife.Work.Messages.Events.Company;

public record SalaryPaid(Guid CorrelationId, float Amount, float TaxDeducted) : CorrelatedBy<Guid>;