using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Job;
using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record CreateJobPosting(Guid CorrelationId, JobPosting JobPosting) : CorrelatedBy<Guid>;