using Genelife.Domain;
using Genelife.Domain.Work;
using Genelife.Domain.Work.Job;
using MassTransit;

namespace Genelife.Messages.Events.Jobs;

public record JobApplicationSubmitted(Guid CorrelationId, JobApplication Application) : CorrelatedBy<Guid>;