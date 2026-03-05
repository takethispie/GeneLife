using Genelife.Domain;
using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record Recruit(Guid CorrelationId, Guid JobPostingId, JobPosting JobPosting, float salary) : CorrelatedBy<Guid>;