using MassTransit;

namespace Genelife.Domain.Commands;

public record TransferHourlyPay(Guid CorrelationId, float Amount) : CorrelatedBy<Guid>;