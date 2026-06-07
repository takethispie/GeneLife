using Domain.ValueObjects;

namespace Domain.Entities;

public class House(Guid id, Address address) {
    public Guid Id { get; private set; } = id;
    public Address Address { get; private set; } = address;
    public string? Name { get; private set; } = null;
}