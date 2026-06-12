using Domain.Enums;
using Domain.Repositories;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Human {
    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Needs Needs { get; private set; }
    public float Money { get; private set; }
    public Address Address { get; private set; }
    public HumanState State { get; private set; } = HumanState.Idle;

    public Human(Guid id, string firstName, string lastName, Needs needs, float money, Address address)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Needs = needs;
        Money = money;
        Address = address;
    }

    public Human()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        Needs = new Needs(0, 0, 0);
        Money = 0;
        Address = new Address();
    }
    
    public void MoveHome(Address newAddress, IHouseRepository houseRepository)
    {
        Address = newAddress;
    }
}