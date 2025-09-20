using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Commands.Jobs;

public record Recruit(Guid CorrelationId, JobPosting JobPosting, float salary) : CorrelatedBy<Guid>;