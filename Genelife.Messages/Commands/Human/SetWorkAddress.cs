using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetWorkAddress(Guid CorrelationId, Guid OfficeId, Position OfficeLocation) : CorrelatedBy<Guid>;