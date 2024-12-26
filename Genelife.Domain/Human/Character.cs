using Genelife.Domain.Items;
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
    public Career CurrentCareer { get; private set; }
    public decimal Money { get; set; }
    public Dictionary<string, float> Skills { get; }
    public DateTime LastWorkDay { get; private set; }
    public bool IsWorking { get; private set; }
    public Inventory Inventory { get; }
    

    private readonly Random random;
    private readonly List<Activity> availableActivities;

    public Character(Guid id, string firstName, string lastName, int age, Sex sex)
    {
        CorrelationId = id;
        FirstName = firstName;
        LastName = lastName;
        Age = age;
        Sex = sex;
        random = new();
        Relationships = new();
        CurrentMood = Mood.Happy;
        Inventory = new ();

        // Initialize needs with different decay rates
        Needs = new() {
            { NeedType.Hunger, new(NeedType.Hunger, 0.05f) },
            { NeedType.Energy, new(NeedType.Energy, 0.03f) },
            { NeedType.Social, new(NeedType.Social, 0.02f) },
            { NeedType.Hygiene, new(NeedType.Hygiene, 0.04f) },
            { NeedType.Fun, new(NeedType.Fun, 0.03f) }
        };

        // Initialize personality traits
        Traits = new() {
            { "Sociability", random.NextSingle() },
            { "EnergyLevel", random.NextSingle() },
            { "Cleanliness", random.NextSingle() }
        };

        // Initialize available activities
        availableActivities = InitializeActivities();
    }

    private List<Activity> InitializeActivities() => [
        new("Eat", 30,
            new() { { NeedType.Hunger, 50f } },
            new() { { Mood.Happy, 0.3f }, { Mood.Energetic, 0.2f } }
        ),

        new("Sleep", 480,
            new() { { NeedType.Energy, 100f } },
            new() { { Mood.Energetic, 0.8f } }
        ),

        new("Shower", 20,
            new() { { NeedType.Hygiene, 70f } },
            new() { { Mood.Happy, 0.4f } }
        ),

        new("Watch TV", 60,
            new() {
                { NeedType.Fun, 30f },
                { NeedType.Energy, -10f }
            },
            new() {
                { Mood.Happy, 0.3f },
                { Mood.Tired, 0.2f }
            }
        )
    ];

    public void UpdateNeeds(float minutes)
    {
        foreach (var need in Needs.Values)
        {
            need.Decay(minutes);
        }
    }

    public void PerformActivity(Activity activity)
    {
        CurrentActivity = activity;

        // Update needs based on activity
        foreach (var effect in activity.NeedEffects) {
            Needs[effect.Key].Modify(effect.Value);
        }

        // Update mood based on activity
        var strongestMoodEffect = activity.MoodEffects.OrderByDescending(x => x.Value).First();
        if (strongestMoodEffect.Value > 0.5f)
            CurrentMood = strongestMoodEffect.Key;
    }

    public Activity ChooseNextActivity()
    {
        // Find the need with lowest value
        var lowestNeed = Needs.OrderBy(n => n.Value.Value).First();

        // Filter activities that address the most pressing need
        var relevantActivities = availableActivities
            .Where(a => a.NeedEffects.ContainsKey(lowestNeed.Key))
            .ToList();

        return relevantActivities.Count != 0
            ? relevantActivities[random.Next(relevantActivities.Count)]
            : availableActivities[random.Next(availableActivities.Count)];
    }
    

    public string GetStatusReport()
    {
        var report = $"Status Report for {FirstName}:\n";
        report += $"Current Mood: {CurrentMood}\n";
        report += "Needs:\n";
        
        foreach (var need in Needs)
        {
            report += $"  {need.Key}: {need.Value.Value:F1}%\n";
        }

        if (CurrentActivity != null)
            report += $"Current Activity: {CurrentActivity.Name}\n";
        return report;
    }

    public void ActivityTick() {
        if (CurrentActivity == null) return;
        CurrentActivity.Duration -= 1;
        if (CurrentActivity.Duration <= 0) CurrentActivity = null;
    }
    
    public void ApplyForJob(Career career)
    {
            // Check if meets minimum requirements for level 1
            var entryLevel = career.CareerLevels.First();
            var qualifies = entryLevel.RequiredSkills.All(
                skill => Skills.ContainsKey(skill.Key) && Skills[skill.Key] >= skill.Value
            );

            if (qualifies)
            {
                CurrentCareer = career;
                Money += 100m; // Signing bonus
            }
            else throw new InvalidOperationException("Does not meet minimum job requirements");
    }

    public void WorkShift(DateTime currentTime)
    {
        if (!CurrentCareer.WorkDays.Contains(currentTime.DayOfWeek)) return;
        if (LastWorkDay.Date == currentTime.Date) return;

        IsWorking = true;
        
        // Work effects
        Needs[NeedType.Energy].Modify(-30f);
        Needs[NeedType.Fun].Modify(-20f);
        
        // Earn money
        decimal hoursWorked = (decimal)CurrentCareer.WorkingHours.TotalHours;
        decimal dailyPay = CurrentCareer.HourlyPay * hoursWorked;
        Money += dailyPay;

        // Skill improvement chance
        foreach (var skill in CurrentCareer.RequiredSkills)
        {
            if (random.NextDouble() < 0.3) // 30% chance to improve relevant skill
                ImproveSkill(skill.Key, 0.1f);
        }

        // Try for promotion
        if (random.NextDouble() < 0.1) // 10% chance to check for promotion each day
            TryPromotion();

        LastWorkDay = currentTime.Date;
        IsWorking = false;
    }

    public void ImproveSkill(string skillName, float amount)
    {
        if (!Skills.TryGetValue(skillName, out var value))
        {
            value = 0f;
            Skills[skillName] = value;
        }
        Skills[skillName] = Math.Min(10f, value + amount);
    }

    public void TryPromotion() {
        if (!(CurrentCareer?.TryPromote(Skills) ?? false)) return;
        CurrentMood = Mood.Happy;
        Money += 500; // Promotion bonus
    }
    
    public bool UseItem(string itemName)
    {
        var item = Inventory.FindItem(itemName);
        if (!item.Use()) return false;
        // Apply item effects
        foreach (var effect in item.UseEffects)
        {
            Needs[effect.Key].Modify(effect.Value);
        }

        // Remove consumed items
        if (item.Durability <= 0)
            Inventory.RemoveItem(itemName);
        return true;
    }

    public void SellItem(string itemName, int quantity = 1)
    {
        var item = Inventory.FindItem(itemName);
        if (Inventory.RemoveItem(itemName, quantity))
            Money += item.Value * quantity;
    }

    public bool BuyItem(Item item)
    {
        if (Money < item.Value || Inventory.RemainingSlots <= 0) return false;
        Money -= item.Value;
        return Inventory.AddItem(item);
    }
}