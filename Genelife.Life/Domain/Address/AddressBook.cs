namespace Genelife.Life.Domain.Address;

public class AddressBook
{
    public List<AddressEntry> Addresses = [];

    public void Add(AddressEntry entry)
    {
        //TODO checks for type that should have only one address (like own home)
        if (Addresses.Any(address => address.EntityId == entry.EntityId)) return;
        Addresses.Add(entry);
    }
    
    public bool Remove(AddressEntry entry) => Addresses.Remove(entry);
    
    public bool Exists(AddressEntry entry) => Addresses.Any(ad => ad == entry);
    
    public IEnumerable<AddressEntry> AllOfAddressType(AddressType addressType) => Addresses.Where(ad => ad.AddressType == addressType);
}