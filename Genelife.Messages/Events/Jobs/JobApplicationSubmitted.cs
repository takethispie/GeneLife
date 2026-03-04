using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Events.Jobs;

public record JobApplicationSubmitted(Guid CorrelationId, JobApplication Application) : CorrelatedBy<Guid>;