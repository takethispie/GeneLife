using Genelife.Global.Messages.DTOs;

namespace Genelife.Global.Domain.Address;

public record AddressEntry(Position Position, AddressType AddressType, Guid EntityId);