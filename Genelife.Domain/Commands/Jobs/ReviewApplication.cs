using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Commands.Jobs;

public record ReviewApplication(
    Guid CorrelationId,
    Guid ApplicationId,
    ApplicationStatus NewStatus,
    string? ReviewNotes = null,
    decimal? OfferedSalary = null
) : CorrelatedBy<Guid>;