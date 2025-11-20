using MassTransit;

namespace Genelife.Work.Messages.Commands.Jobs;

public record RemoveApplication(Guid CorrelationId, Guid CompanyId) : CorrelatedBy<Guid>;