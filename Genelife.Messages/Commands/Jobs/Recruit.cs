using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Job;
using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record Recruit(Guid CorrelationId, Guid JobPostingId, JobPosting JobPosting, float Salary) : CorrelatedBy<Guid>;