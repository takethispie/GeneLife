using System.Numerics;
using Genelife.Domain.Address.Exceptions;

namespace Genelife.Domain.Address;

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

    public AddressEntry? NearestOfAddressType(AddressType addressType, float x, float y, float z)
    {
        var origin = new Vector3(x, y, z);
        return Addresses
            .Where(ad => ad.AddressType == addressType)
            .OrderBy(ad => Vector3.Distance(origin, new Vector3(ad.Coordinates.X, ad.Coordinates.Y, ad.Coordinates.Z)))
            .FirstOrDefault();
    }
    
    public AddressEntry GetHomeAddress()
    {
        var homeAddress = AllOfAddressType(AddressType.Home).FirstOrDefault();
        return homeAddress is null ? throw new AddressNotFoundException(nameof(homeAddress)) : homeAddress;
    }
    
    public AddressEntry GetWorkAddress()
    {
        var officeAddress = AllOfAddressType(AddressType.Office).FirstOrDefault();
        return officeAddress is null ? throw new AddressNotFoundException(nameof(officeAddress)) : officeAddress;
    }
}