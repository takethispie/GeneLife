using Genelife.Work.Messages.Events.Company;

namespace Genelife.Life.Domain.Address;

public record AddressEntry(AddressType AddressType, Guid EntityId, AddressCoordinates Coordinates);