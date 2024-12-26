using Genelife.Domain.Human;
using MassTransit;

namespace Genelife.Domain.Commands.Create;

public record CreateHuman(Guid CorrelationId, Character Human, int X, int Y, int Hunger = 0, int Thirst = 0) : CorrelatedBy<Guid>;