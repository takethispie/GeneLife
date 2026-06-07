using Domain.ValueObjects;

namespace Domain.Entities;

public class Company(Guid id, string name, List<Guid> employees, DateOnly founded, Address address) {
    public Guid Id { get; private set; } = id;
    public string Name { get; private set; } = name;
    public DateOnly Founded { get; private set; } = founded;
    public List<Guid> Employees { get; private set; } = employees;
    public Address Address { get; private set; } = address;
}