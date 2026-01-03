using MassTransit;

namespace Genelife.Work.Messages.Events.Jobs;

public record RecruitmentAccepted(Guid CorrelationId, Guid WorkerId, Guid CompanyId, float Salary) : CorrelatedBy<Guid>;