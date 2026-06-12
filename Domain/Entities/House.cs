using Domain.ValueObjects;

namespace Domain.Entities;

public class House {
    public Guid Id { get; private set; }
    public Address Address { get; private set; }
    public string? Name { get; private set; } = null;

    public House(Guid id, Address address)
    {
        Id = id;
        Address = address;
    }

    public House()
    {
        Address = new Address();
    }
}