using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record Recruit(Guid CorrelationId, Guid JobPostingId, JobPosting JobPosting, float salary) : CorrelatedBy<Guid>;