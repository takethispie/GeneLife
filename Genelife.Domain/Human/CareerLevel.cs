namespace Genelife.Domain.Human;

public class CareerLevel
{
    public int Level { get; }
    public string Title { get; }
    public decimal HourlyPay { get; }
    public Dictionary<string, float> RequiredSkills { get; }

    public CareerLevel(int level, string title, decimal hourlyPay, Dictionary<string, float> requiredSkills)
    {
        Level = level;
        Title = title;
        HourlyPay = hourlyPay;
        RequiredSkills = requiredSkills;
    }
}