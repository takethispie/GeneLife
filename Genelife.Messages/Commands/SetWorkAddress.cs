using Genelife.Domain;
using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Messages.Commands;

public record SetWorkAddress(Guid CorrelationId, Guid OfficeId, OfficeLocation OfficeLocation) : CorrelatedBy<Guid>;