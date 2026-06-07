using Domain.ValueObjects;

namespace Domain.Entities;

public class Human(Guid id, string firstName, string lastName, Needs needs, float money, Address address) {
    public Guid Id { get; set; } = id;
    public string FirstName { get; set; } = firstName;
    public string LastName { get; set; } = lastName;
    public Needs Needs { get; set; } = needs;
    public float Money { get; set; } = money;
    public Address Address { get; set; } = address;
}