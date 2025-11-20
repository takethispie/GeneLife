using MassTransit;

namespace Genelife.Work.Messages.Events.Jobs;

public record RecruitmentAccepted(Guid CorrelationId, Guid HumanId, Guid CompanyId, float Salary) : CorrelatedBy<Guid>;