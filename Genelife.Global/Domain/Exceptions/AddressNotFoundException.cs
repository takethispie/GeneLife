namespace Genelife.Global.Domain.Exceptions;

public class AddressNotFoundException(string addressName) : Exception(addressName);