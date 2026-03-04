using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Commands.Jobs;

public record CreateJobPosting(Guid CorrelationId, JobPosting JobPosting) : CorrelatedBy<Guid>;