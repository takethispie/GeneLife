namespace Genelife.Domain.Human;

public class Career
{
    public string Name { get; }
    public int Level { get; private set; }
    public decimal HourlyPay { get; private set; }
    public Dictionary<string, float> RequiredSkills { get; }
    public List<CareerLevel> CareerLevels { get; }
    public TimeSpan WorkingHours { get; }
    public DayOfWeek[] WorkDays { get; }

    public Career(string name, decimal startingPay, TimeSpan workingHours, DayOfWeek[] workDays)
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
            new(1, "Intern", 15.0m, new() { { "Writing", 1 }, { "Communication", 1 } }),
            new(2, "Junior", 20.0m, new() { { "Writing", 2 }, { "Communication", 2 } }),
            new(3, "Associate", 25.0m, new() { { "Writing", 3 }, { "Communication", 3 }, { "Leadership", 1 } }),
            new(4, "Senior", 35.0m, new() { { "Writing", 4 }, { "Communication", 4 }, { "Leadership", 2 } }),
            new(5, "Manager", 45.0m, new() { { "Writing", 4 }, { "Communication", 5 }, { "Leadership", 4 } })
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