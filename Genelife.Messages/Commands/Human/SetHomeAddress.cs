using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Commands.Human;

public record SetHomeAddress(Guid CorrelationId, Guid HomeId, Position Coordinates) : CorrelatedBy<Guid>;