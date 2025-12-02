using MassTransit;

namespace Genelife.Work.Messages.Events.Jobs;

public record RecruitmentRefused(Guid CorrelationId, Guid HumanId) : CorrelatedBy<Guid>;