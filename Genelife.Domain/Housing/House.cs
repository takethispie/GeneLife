using Genelife.Domain.Human;
using MassTransit;

namespace Genelife.Domain.Housing;

public class House : CorrelatedBy<Guid>
{
    public Guid CorrelationId { get; }
    public string Name { get; set; }
    public Address Address { get; }
    public List<Room> Rooms { get; }
    public float Value { get; private set; }
    public float MonthlyBills { get; private set; }
    public Character Owner { get; private set; }
    public bool IsForSale { get; set; }
    public Dictionary<NeedType, float> ComfortModifiers { get; private set; }

    public House(Guid id, string name, Address address, float value) {
        CorrelationId = id;
        Name = name;
        Address = address;
        Rooms = new List<Room>();
        Value = value;
        IsForSale = true;
        ComfortModifiers = new Dictionary<NeedType, float>();
        MonthlyBills = value * 0.001f; // Basic monthly bills calculation
    }

    public void CalculateValue()
    {
        var baseValue = Value;
        var furnishingValue = Rooms.Sum(r => r.GetFurnitureValue());
        var qualityModifier = 1 + (Rooms.Average(r => r.GetQualityScore()) * 0.2f);
        
        Value = (baseValue + furnishingValue) * qualityModifier;
        MonthlyBills = Value * 0.001f + (Rooms.Count * 50); // Bills increase with house value and room count
    }

    public void UpdateComfortModifiers()
    {
        ComfortModifiers.Clear();
        foreach (var room in Rooms) {
            foreach (var modifier in room.ComfortModifiers)
            {
                if (!ComfortModifiers.ContainsKey(modifier.Key))
                    ComfortModifiers[modifier.Key] = 0;
                ComfortModifiers[modifier.Key] += modifier.Value;
            }
        }
    }

    public bool SetOwner(Character character)
    {
        if (!IsForSale || character.Money < Value)
            return false;
        Owner = character;
        IsForSale = false;
        character.Money -= Value;
        character.OwnedProperties.Add(CorrelationId);
        return true;
    }
}