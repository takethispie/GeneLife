using Genelife.Domain.Housing;
using Genelife.Domain.Items;
using Genelife.Domain.Work;
using MassTransit;

namespace Genelife.Domain.Human;

public class Character : CorrelatedBy<Guid>
{
    public int Age { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; private set; }
    public Dictionary<NeedType, Need> Needs { get; }
    public Mood CurrentMood { get; private set; }
    public Activity? CurrentActivity { get; private set; }
    public Dictionary<string, float> Relationships { get; }
    public Dictionary<string, float> Traits { get; }
    public Guid CorrelationId { get; init; }
    public Sex Sex { get; init; }
    public Career? CurrentCareer { get; private set; }
    public float Money { get; set; }
    public Dictionary<string, float> Skills { get; }
    public DateTime LastWorkDay { get; private set; }
    public bool IsWorking { get; private set; }
    public Inventory Inventory { get; }
    public List<Guid> OwnedProperties { get; }
    public ILocalizable CurrentLocation { get; private set; }
    public Room CurrentRoom { get; private set; }
    
    private readonly List<Activity> availableActivities;

    public Character(Guid id, string firstName, string lastName, int age, Sex sex)
    {
        CorrelationId = id;
        FirstName = firstName;
        LastName = lastName;
        Age = age;
        Sex = sex;
        Relationships = new();
        CurrentMood = Mood.Happy;
        Inventory = new ();
        Needs = new() {
            { NeedType.Hunger, new(NeedType.Hunger, 0.05f) },
            { NeedType.Energy, new(NeedType.Energy, 0.03f) },
            { NeedType.Social, new(NeedType.Social, 0.02f) },
            { NeedType.Hygiene, new(NeedType.Hygiene, 0.04f) },
            { NeedType.Fun, new(NeedType.Fun, 0.03f) }
        };
        var random = new Random();
        Traits = new() {
            { "Sociability", random.NextSingle() },
            { "EnergyLevel", random.NextSingle() },
            { "Cleanliness", random.NextSingle() }
        };
        availableActivities = ActivityFactory.InitializeBasicHumanActivities();
    }
    

    public void UpdateNeeds(float minutes)
    {
        foreach (var need in Needs.Values) {
            need.Decay(minutes);
        }
    }
    

    public void PerformActivity(Activity activity)
    {
        CurrentActivity = activity;
        foreach (var effect in activity.NeedEffects) {
            Needs[effect.Key].Modify(effect.Value);
        }
        var strongestMoodEffect = activity.MoodEffects.OrderByDescending(x => x.Value).First();
        if (strongestMoodEffect.Value > 0.5f)
            CurrentMood = strongestMoodEffect.Key;
    }
    

    public Activity ChooseNextActivity()
    {
        var lowestNeed = Needs.OrderBy(n => n.Value.Value).First();
        var relevantActivities = availableActivities
            .Where(a => a.NeedEffects.ContainsKey(lowestNeed.Key))
            .ToList();
        var random = new Random();
        return relevantActivities.Count != 0
            ? relevantActivities[random.Next(relevantActivities.Count)]
            : availableActivities[random.Next(availableActivities.Count)];
    }
    

    public string GetStatusReport()
    {
        var report = $"Status Report for {FirstName}:\n";
        report += $"Current Mood: {CurrentMood}\n";
        report += "Needs:\n";
        foreach (var need in Needs) {
            report += $"  {need.Key}: {need.Value.Value:F1}%\n";
        }
        if (CurrentActivity != null) report += $"Current Activity: {CurrentActivity.Name}\n";
        return report;
    }
    

    public void ActivityTick() {
        if (CurrentActivity == null) return;
        CurrentActivity.Duration -= 1;
        if (CurrentActivity.Duration <= 0) CurrentActivity = null;
    }
    
    
    public void ApplyForJob(Career career)
    {
        var entryLevel = career.CareerLevels.First();
        var qualifies = entryLevel.RequiredSkills.All(
            skill => Skills.ContainsKey(skill.Key) && Skills[skill.Key] >= skill.Value
        );
        if (qualifies) {
            CurrentCareer = career;
            Money += 100;
        }
        else throw new InvalidOperationException("Does not meet minimum job requirements");
    }
    

    public void WorkShift(DateTime currentTime)
    {
        if(CurrentCareer == null) return;
        if (!CurrentCareer.WorkDays.Contains(currentTime.DayOfWeek)) return;
        if (LastWorkDay.Date == currentTime.Date) return;
        IsWorking = true;
        Needs[NeedType.Energy].Modify(-30f);
        Needs[NeedType.Fun].Modify(-20f);
        var dailyPay = CurrentCareer.HourlyPay * CurrentCareer.WorkingHours;
        Money += dailyPay;
        var random = new Random();
        foreach (var skill in CurrentCareer.RequiredSkills) {
            if (random.NextDouble() < 0.3)
                ImproveSkill(skill.Key, 0.1f);
        }

        if (random.NextDouble() < 0.1) 
            TryPromotion();

        LastWorkDay = currentTime.Date;
        IsWorking = false;
    }
    

    public void ImproveSkill(string skillName, float amount)
    {
        if (!Skills.TryGetValue(skillName, out var value)) {
            value = 0f;
            Skills[skillName] = value;
        }
        Skills[skillName] = Math.Min(10f, value + amount);
    }
    

    public void TryPromotion() {
        if (!(CurrentCareer?.TryPromote(Skills) ?? false)) return;
        CurrentMood = Mood.Happy;
        Money += 500;
    }
    
    
    public bool UseItem(string itemName)
    {
        var item = Inventory.FindItem(itemName);
        if(item == null || !item.Use()) return false;
        foreach (var effect in item.UseEffects) {
            Needs[effect.Key].Modify(effect.Value);
        }
        if (item.Durability <= 0) Inventory.RemoveItem(itemName);
        return true;
    }
    

    public void SellItem(string itemName, int quantity = 1)
    {
        var item = Inventory.FindItem(itemName);
        if (Inventory.RemoveItem(itemName, quantity)) Money += item.Value * quantity;
    }
    

    public bool BuyItem(Item item)
    {
        if (Money < item.Value || Inventory.RemainingSlots <= 0) return false;
        Money -= item.Value;
        return Inventory.AddItem(item);
    }
    
    
    public bool BuyHouse(House house)
    {
        if (!house.SetOwner(this)) return false;
        CurrentLocation = house;
        return true;
    }
    

    public bool SellHouse(House house)
    {
        if (!OwnedProperties.Contains(house.CorrelationId))
            return false;
        OwnedProperties.Remove(house.CorrelationId);
        Money += house.Value;
        house.IsForSale = true;
        if (CurrentLocation == house)
            CurrentLocation = null;
        return true;
    }

    public void EnterRoom(Room room) {
        if (CurrentLocation is not House) return;
        //TODO check for room existing in house
        CurrentRoom = room;
        foreach (var modifier in room.ComfortModifiers) {
            Needs[modifier.Key].Modify(modifier.Value * 0.1f);
        }
    }
}