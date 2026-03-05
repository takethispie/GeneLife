namespace Genelife.Domain.Address;

public record AddressEntry(AddressType AddressType, Guid EntityId, AddressCoordinates Coordinates);