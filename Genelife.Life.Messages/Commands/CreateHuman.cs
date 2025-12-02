using Genelife.Life.Messages.DTOs;
using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record CreateHuman(Guid CorrelationId, Human Human) : CorrelatedBy<Guid>;