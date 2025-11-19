using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record RecruitmentAccepted(Guid CorrelationId, Guid HumanId, Guid CompanyId, float Salary) : CorrelatedBy<Guid>;