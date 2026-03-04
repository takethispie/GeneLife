using MassTransit;

namespace Genelife.Messages.Events.Jobs;

public record RecruitmentRefused(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;