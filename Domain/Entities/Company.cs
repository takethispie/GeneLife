using Domain.ValueObjects;

namespace Domain.Entities;

public class Company
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateOnly Founded { get; private set; }
    public List<Guid> Employees { get; private set; }
    public Address Address { get; private set; }

    public Company(Guid id, string name, List<Guid> employees, DateOnly founded, Address address)
    {
        Id = id;
        Name = name;
        Founded = founded;
        Employees = employees;
        Address = address;
    }

    public Company()
    {
        Name = string.Empty;
        Employees = [];
        Address = new Address();
    }
}