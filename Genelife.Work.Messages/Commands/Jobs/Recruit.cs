using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Work.Messages.Commands.Jobs;

public record Recruit(Guid CorrelationId, Guid JobPostingId, JobPosting JobPosting, float salary) : CorrelatedBy<Guid>;