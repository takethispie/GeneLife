using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record JobApplicationSubmitted(Guid CorrelationId, JobApplication Application) : CorrelatedBy<Guid>;