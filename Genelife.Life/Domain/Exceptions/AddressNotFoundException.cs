namespace Genelife.Life.Domain.Exceptions;

public class AddressNotFoundException(string addressName) : Exception(addressName);