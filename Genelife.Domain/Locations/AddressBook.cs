using System.Numerics;
using Genelife.Domain.Locations.Exceptions;

namespace Genelife.Domain.Locations;

public class AddressBook
{
    public List<Address> Addresses { get; init; } = [];

    private void Add(Address entry)
    {
        //TODO checks for type that should have only one address (like own home)
        if (Addresses.Any(address => address.EntityId == entry.EntityId)) return;
        Addresses.Add(entry);
    }
    
    public bool Remove(Address entry) => Addresses.Remove(entry);
    
    public bool Exists(Address entry) => Addresses.Any(ad => ad == entry);
    
    public IEnumerable<Address> AllOfAddressType(AddressType addressType) => Addresses.Where(ad => ad.AddressType == addressType);

    public Address? NearestOfAddressType(AddressType addressType, float x, float y, float z)
    {
        var origin = new Vector3(x, y, z);
        return Addresses
            .Where(ad => ad.AddressType == addressType)
            .OrderBy(ad => Vector3.Distance(origin, new Vector3(ad.Coordinates.X, ad.Coordinates.Y, ad.Coordinates.Z)))
            .FirstOrDefault();
    }
    
    public Address GetHomeAddress()
    {
        var homeAddress = AllOfAddressType(AddressType.Home).FirstOrDefault();
        return homeAddress is null ? throw new AddressNotFoundException(nameof(homeAddress)) : homeAddress;
    }
    
    public Address GetWorkAddress()
    {
        var officeAddress = AllOfAddressType(AddressType.Office).FirstOrDefault();
        return officeAddress is null ? throw new AddressNotFoundException(nameof(officeAddress)) : officeAddress;
    }

    public void AddWorkAddress(float x, float y, float z, Guid id)
    {
        var coordinates = new AddressCoordinates(x, y, z);
        Add(new Address(AddressType.Office, id, coordinates));
    }
    
    public void AddHomeAddress(float x, float y, float z, Guid id)
    {
        var coordinates = new AddressCoordinates(x, y, z);
        Add(new Address(AddressType.Home, id, coordinates));
    }

    public void AddGroceryStore(float x, float y, float z, Guid id)
    {
        var coordinates = new AddressCoordinates(x, y, z);
        Add(new Address(AddressType.Store, id, coordinates));
    }
    
    public Guid NearestBuildingId(AddressType type, Position coordinates)
    {
        var res = NearestOfAddressType(type, coordinates.X, coordinates.Y, coordinates.Z);
        return res?.EntityId ?? Guid.Empty;
    }
}