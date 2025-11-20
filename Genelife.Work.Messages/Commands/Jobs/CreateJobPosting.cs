using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Work.Messages.Commands.Jobs;

public record CreateJobPosting(Guid CorrelationId, JobPosting JobPosting) : CorrelatedBy<Guid>;