using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Commands.Jobs;

public record CreateJobPosting(Guid CorrelationId, JobPosting JobPosting) : CorrelatedBy<Guid>;