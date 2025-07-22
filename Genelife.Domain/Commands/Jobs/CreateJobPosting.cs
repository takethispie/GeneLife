using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Commands.Jobs;

public record CreateJobPosting(Guid CorrelationId, Guid CompanyId, JobPosting JobPosting) : CorrelatedBy<Guid>;