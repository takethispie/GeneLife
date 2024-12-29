using Genelife.Domain;
using Genelife.Domain.Interfaces;

namespace Genelife.Main.Domain.Activities;

public class Work(float dailySalary) : IActivity {
    
    public int TickDuration { get; set; } = Constants.TickPerHour * 6;
    public float DailySalary { get; init; } = dailySalary;

    public Human Apply(Human being) => being with {
        Energy = Math.Clamp(being.Energy - 40, 0, 100),
        Money = being.Money + DailySalary,
    };
}