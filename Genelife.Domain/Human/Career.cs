namespace Genelife.Domain.Human;

public class Career
{
    public string Name { get; }
    public int Level { get; private set; }
    public float HourlyPay { get; private set; }
    public Dictionary<string, float> RequiredSkills { get; }
    public List<CareerLevel> CareerLevels { get; }
    public int WorkingHours { get; }
    public DayOfWeek[] WorkDays { get; }

    public Career(string name, float startingPay, int workingHours, DayOfWeek[] workDays)
    {
        Name = name;
        Level = 1;
        HourlyPay = startingPay;
        RequiredSkills = new();
        CareerLevels = InitializeCareerLevels();
        WorkingHours = workingHours;
        WorkDays = workDays;
    }

    private List<CareerLevel> InitializeCareerLevels()
    {
        // Example career progression
        return new() {
            new(1, "Intern", 15, new() { { "Writing", 1 }, { "Communication", 1 } }),
            new(2, "Junior", 20, new() { { "Writing", 2 }, { "Communication", 2 } }),
            new(3, "Associate", 25, new() { { "Writing", 3 }, { "Communication", 3 }, { "Leadership", 1 } }),
            new(4, "Senior", 35, new() { { "Writing", 4 }, { "Communication", 4 }, { "Leadership", 2 } }),
            new(5, "Manager", 45, new() { { "Writing", 4 }, { "Communication", 5 }, { "Leadership", 4 } })
        };
    }

    public bool TryPromote(Dictionary<string, float> currentSkills)
    {
        var nextLevel = CareerLevels.FirstOrDefault(l => l.Level == Level + 1);
        if (nextLevel == null) return false;

        // Check if all required skills are met
        foreach (var requiredSkill in nextLevel.RequiredSkills)
        {
            if (!currentSkills.ContainsKey(requiredSkill.Key) ||
                currentSkills[requiredSkill.Key] < requiredSkill.Value)
                return false;
        }

        Level++;
        HourlyPay = nextLevel.HourlyPay;
        return true;
    }
}