using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Work.Messages.Events.Jobs;

public record JobApplicationSubmitted(Guid CorrelationId, JobApplication Application) : CorrelatedBy<Guid>;