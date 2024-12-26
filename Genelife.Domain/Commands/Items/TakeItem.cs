using Genelife.Domain.Items;
using MassTransit;

namespace Genelife.Domain.Commands.Items;

public record TakeItem(Guid CorrelationId, Item Item) : CorrelatedBy<Guid>; 