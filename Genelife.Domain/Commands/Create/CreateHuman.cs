using MassTransit;

namespace Genelife.Domain.Commands;

public record CreateHuman(Guid CorrelationId, Human Human, int X, int Y, int Hunger = 0, int Thirst = 0) : CorrelatedBy<Guid>;