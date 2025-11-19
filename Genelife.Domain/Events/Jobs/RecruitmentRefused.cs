using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record RecruitmentRefused(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;