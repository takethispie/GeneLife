using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Commands;

public record SetHomeAddress(Guid HumanId, Guid HomeId);