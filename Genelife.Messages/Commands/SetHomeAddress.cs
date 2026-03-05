using Genelife.Domain;
using MassTransit;

namespace Genelife.Messages.Commands;

public record SetHomeAddress(Guid CorrelationId, Guid HomeId, Position Coordinates) : CorrelatedBy<Guid>;