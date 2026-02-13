using Genelife.Work.Messages.DTOs;
using MassTransit;

namespace Genelife.Life.Messages.Commands;

public record SetWorkAddress(Guid CorrelationId, Guid OfficeId, OfficeLocation OfficeLocation) : CorrelatedBy<Guid>;