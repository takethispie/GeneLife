namespace Genelife.Domain.Exceptions;

public class AddressNotFoundException(string addressName) : Exception(addressName);