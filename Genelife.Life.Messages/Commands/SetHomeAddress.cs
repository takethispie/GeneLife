using Genelife.Life.Messages.DTOs;
using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetHomeAddress(Guid CorrelationId, Guid HomeId, Coordinates Coordinates) : CorrelatedBy<Guid>;