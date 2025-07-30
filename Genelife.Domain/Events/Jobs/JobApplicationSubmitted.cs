using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Events.Jobs;

public record JobApplicationSubmitted(Guid CorrelationId, Guid HumanId, JobApplication Application) : CorrelatedBy<Guid>;