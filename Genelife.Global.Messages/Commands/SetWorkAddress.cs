using System.Numerics;
using MassTransit;

namespace Genelife.Global.Messages.Commands;

public record SetWorkAddress(Guid HumanId, Guid OfficeId);