using MassTransit;

namespace Genelife.Messages.Events.Jobs;

public record RecruitmentAccepted(Guid CorrelationId, Guid WorkerId, Guid CompanyId, float Salary) : CorrelatedBy<Guid>;