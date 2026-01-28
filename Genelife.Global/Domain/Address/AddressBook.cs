namespace Genelife.Global.Domain.Address;

public class AddressBook
{
    private List<AddressEntry> adresses = [];

    public void Add(AddressEntry entry)
    {
        //TODO checks for type that should have only one address (like own home)
        if (adresses.Any(address => address.Position == entry.Position)) return;
        adresses.Add(entry);
    }
    
    public bool Remove(AddressEntry entry) => adresses.Remove(entry);
    
    public bool Exists(AddressEntry entry) => adresses.Any(ad => ad == entry);
    
    public IEnumerable<AddressEntry> AllOfAddressType(AddressType addressType) => adresses.Where(ad => ad.AddressType == addressType);
}