using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record RemoveApplication(Guid CorrelationId, Guid CompanyId) : CorrelatedBy<Guid>;