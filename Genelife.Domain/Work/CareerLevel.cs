namespace Genelife.Domain.Work;

public class CareerLevel(int level, string title, float hourlyPay, Dictionary<string, float> requiredSkills) {
    public int Level { get; } = level;
    public string Title { get; } = title;
    public float HourlyPay { get; } = hourlyPay;
    public Dictionary<string, float> RequiredSkills { get; } = requiredSkills;
}