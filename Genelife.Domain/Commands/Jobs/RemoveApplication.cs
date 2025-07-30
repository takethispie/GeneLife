using MassTransit;

namespace Genelife.Domain.Commands.Jobs;

public record RemoveApplication(Guid CorrelationId, Guid CompanyId) : CorrelatedBy<Guid>;