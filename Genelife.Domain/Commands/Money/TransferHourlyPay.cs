using MassTransit;

namespace Genelife.Domain.Commands.Money;

public record TransferHourlyPay(Guid CorrelationId, float Amount) : CorrelatedBy<Guid>;