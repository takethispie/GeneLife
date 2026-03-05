namespace Genelife.Domain.Address.Exceptions;

public class AddressNotFoundException(string addressName) : Exception(addressName);