namespace Genelife.Domain.Locations;

public record Address(AddressType AddressType, Guid EntityId, AddressCoordinates Coordinates);