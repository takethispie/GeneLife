namespace Genelife.Domain.Locations.Exceptions;

public class AddressNotFoundException(string addressName) : Exception(addressName);