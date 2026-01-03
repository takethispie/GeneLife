using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Domain.Address;

public record AddressEntry(Position Position, AddressType AddressType, Guid EntityId);