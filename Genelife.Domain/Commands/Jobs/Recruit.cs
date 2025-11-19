using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Commands.Jobs;

public record Recruit(Guid CorrelationId, Guid JobPostingId, JobPosting JobPosting, float salary) : CorrelatedBy<Guid>;