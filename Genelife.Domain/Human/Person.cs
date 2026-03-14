using Genelife.Domain.Activities;
using Genelife.Domain.Activities.Interfaces;
using Genelife.Domain.Address;
using Genelife.Domain.CheatCodes;
using OneOf;

namespace Genelife.Domain.Human;

public class Person(
    Guid id,
    string firstName,
    string lastName,
    DateTime birthday,
    Sex sex,
    LifeSkillSet lifeSkillSet,
    Position coordinates,
    AddressBook addressBook,
    float money,
    float hunger = 100,
    float energy = 100,
    float hygiene = 100,
    float thirst = 100
)
{
    public Guid Id { get; init; } = id;
    public string FirstName { get; init; } = firstName;
    public string LastName { get; init; } = lastName;
    public DateTime Birthday { get; private set; } = birthday;
    public Sex Sex { get; init; } = sex;
    public LifeSkillSet LifeSkillSet { get; private set; } = lifeSkillSet;
    public Position Coordinates { get; private set; } = coordinates;
    public float Money { get; set; } = money;
    public float Hunger { get; private set; } = hunger;
    public float Energy { get; private set; } = energy;
    public float Hygiene { get; private set; } = hygiene;
    public float Thirst { get; private set; } = thirst;
    public AddressBook  AddressBook { get; init; } = addressBook;
    public int FoodItemCount { get; private set; }
    public int DrinkItemCount { get; private set; }

    public void Do(IBeingActivity activity)
    {
        (Money, Energy, Hunger, Thirst, Hygiene)  = activity switch
        {
            Activities.Work => (Money, Math.Clamp(Energy - 40, 0, 100), Hunger, Thirst, Hygiene),
            Activities.Shower => (Money, Math.Clamp(Energy - 5, 0, 100), Hunger, Thirst, 100),
            Sleep => (Money, 100, Hunger, Thirst, Hygiene),
            Drink => (Money, Energy, Hunger, TakeADrink(), Hygiene),
            Eat => (Money, Energy, EatSomething(), Thirst, Hygiene),
            Idle => (Money, Energy, Hunger, Thirst, Hygiene),
            _ => throw new NotImplementedException()
        };
    }

    private int TakeADrink()
    {
        DrinkItemCount--;
        return 100;
    }

    private int EatSomething()
    {
        FoodItemCount--;
        return 100;
    }

    public void BuyDrink(int amount)
    {
        Money -= amount;
        DrinkItemCount += amount;
    }

    public void BuyFood(int amount)
    {
        Money -= amount;
        FoodItemCount += amount;
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
    
    public IBeingActivity SelectNextActivity(DateTime dateTime, bool works) {
        List<(float val, string name)> actions = [];
        if (dateTime.Hour is >= 22 or <= 1 && Energy < 25)
            actions.Add((Energy, "Energy"));
        if(dateTime.Hour is > 5 and < 8 or > 18 and < 22 && Hygiene < 25)
            actions.Add((Hygiene, "Hygiene"));
        if(dateTime.Hour is > 12 and < 14 or > 18 and < 22 && Hunger < 30)
            actions.Add((Hunger, "Hunger"));
        if(dateTime.Hour is > 10 and < 16 or > 19 and < 23 && Thirst < 30)
            actions.Add((Thirst, "Thirst"));
        if(dateTime.Hour is > 7 and < 18 && Energy > 50 && works)
            actions.Add((Energy, "Work"));
        
        if (actions.Count == 0) return new Idle(dateTime);
        
        return actions.OrderBy(x => x.val).First().name switch {
            "Energy" => new Sleep(dateTime),
            "Hygiene" => new Activities.Shower(dateTime),
            "Hunger" => new Eat(dateTime),
            "Thirst" => new Drink(dateTime),
            "Work" => new Activities.Work(dateTime),
            _ => new Idle(dateTime)
        };
    }

    public int GetFoodBudget()
    {
        var onePercent = Money / 100;
        return Math.Max(Convert.ToInt32(onePercent * 10), 1000);
    }
    
    public int GetDrinkBudget()
    {
        var onePercent = Money / 100;
        return Math.Max(Convert.ToInt32(onePercent * 5), 400);
    }
    
    public void SetPosition(Position pos) => Coordinates = pos;
    
    private static float Decay(float value, float decayRate) => Modify(value, -decayRate * 60);
    
    private static float Modify(float value, float amount) => Math.Clamp(value + amount, 0f, 100f);
}