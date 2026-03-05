using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Address;
using Genelife.Domain.CheatCodes;

namespace Genelife.Domain.Human;

public sealed class Person(
    Guid Id,
    string firstName,
    string lastName,
    DateTime birthday,
    Sex sex,
    LifeSkillSet lifeSkillSet,
    Position coordinates,
    float money,
    float hunger = 100,
    float energy = 100,
    float hygiene = 100,
    float thirst = 100
)
{
    public Guid Id { get; init; } = Id;
    public string FirstName { get; private set; } = firstName;
    public string LastName { get; private set; } = lastName;
    public DateTime Birthday { get; private set; } = birthday;
    public Sex Sex { get; private set; } = sex;
    public LifeSkillSet LifeSkillSet { get; private set; } = lifeSkillSet;
    public Position Coordinates { get; private set; } = coordinates;
    public float Money { get; set; } = money;
    public float Hunger { get; private set; } = hunger;
    public float Energy { get; private set; } = energy;
    public float Hygiene { get; private set; } = hygiene;
    public float Thirst { get; private set; } = thirst;
    public AddressBook  AddressBook { get; } = new AddressBook();

    public void Do(IBeingActivity activity)
    {
        (Money, Energy, Hunger, Thirst, Hygiene)  = activity switch
        {
            Activities.Work => (Money, Math.Clamp(Energy - 40, 0, 100), Hunger, Thirst, Hygiene),
            Activities.Shower => (Money, Math.Clamp(Energy - 5, 0, 100), Hunger, Thirst, 100),
            Sleep => (Money, 100, Hunger, Thirst, Hygiene),
            Drink => (Money, Energy, Hunger, 100, Hygiene),
            Eat => (Money, Energy, 100, Thirst, Hygiene),
            Idle => (Money, Energy, Hunger, Thirst, Hygiene),
            _ => throw new NotImplementedException()
        };
    }

    public void Execute(ICheat cheat)
    {
        switch (cheat)
        {
            case ChangeBirthday changeBirthday:
                Birthday = DateTime.Now.AddYears(-changeBirthday.NewAge);
                break;
            
            case ChangeThirst changeThirst:
                Thirst = changeThirst.Value;
                break;
            
            case ChangeHunger changeHunger:
                Hunger = changeHunger.Value;
                break;
            
            case ChangeEnergy changeEnergy:
                Energy = changeEnergy.Value;
                break;
            
            case ChangeHygiene changeHygiene:
                Hygiene = changeHygiene.Value;
                break;
            
            case ChangeMoney changeMoney:
                Money = changeMoney.Value;
                break;
        }
    }

    public void Update()
    {
        Hunger = Decay(Hunger, 0.05f);
        Energy = Decay(Energy, 0.03f);
        Hygiene = Decay(Hygiene, 0.04f);
    }
    
    public IBeingActivity SelectNextActivity(int hour, bool works) {
        List<(float val, string name)> actions = [];
        if (hour is >= 22 or <= 1 && Energy < 25)
            actions.Add((Energy, "Energy"));
        if(hour is > 5 and < 8 or > 18 and < 22 && Hygiene < 25)
            actions.Add((Hygiene, "Hygiene"));
        if(hour is > 12 and < 14 or > 18 and < 22 && Hunger < 30)
            actions.Add((Hunger, "Hunger"));
        if(hour is > 10 and < 16 or > 19 and < 23 && Thirst < 30)
            actions.Add((Thirst, "Thirst"));
        if(hour is > 7 and < 18 && Energy > 50 && works)
            actions.Add((Energy, "Work"));
        
        if (actions.Count == 0) return new Idle();
        
        return actions.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(),
            "Hygiene" => new Activities.Shower(),
            "Hunger" => new Eat(),
            "Thirst" => new Drink(),
            "Work" => new Activities.Work(),
            _ => new Idle()
        };
    }
    
    private static float Decay(float value, float decayRate) => Modify(value, -decayRate * 60);
    
    private static float Modify(float value, float amount) => Math.Clamp(value + amount, 0f, 100f);
}