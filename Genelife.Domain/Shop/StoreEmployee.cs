using Genelife.Domain.Human;

namespace Genelife.Domain.Shop;

public class StoreEmployee
{
    public Character Character { get; }
    public StorePosition Position { get; private set; }
    public DateTime LastShiftDate { get; private set; }
    public int CustomersServed { get; private set; }
    public float SalesTotal { get; private set; }
    public float Performance { get; private set; }
    public Dictionary<DateTime, int> TimeSheet { get; }

    public StoreEmployee(Character character, StorePosition position)
    {
        Character = character;
        Position = position;
        Performance = 0.75f; // Starting performance
        TimeSheet = new Dictionary<DateTime, int>();
    }

    public void StartShift(DateTime currentTime) {
        if (!Position.IsScheduledToWork(currentTime) || !TimeSheet.TryAdd(currentTime.Date, 0)) return;
        CustomersServed = 0;
        SalesTotal = 0;
    }

    public void EndShift(DateTime currentTime) {
        if (!TimeSheet.ContainsKey(currentTime.Date)) return;
        TimeSheet[currentTime.Date] = Position.ShiftLength;
        LastShiftDate = currentTime.Date;
            
        // Calculate daily performance
        var dailyPerformance = CalculateDailyPerformance();
        Performance = (Performance * 0.7f) + (dailyPerformance * 0.3f); // Rolling average
            
        // Pay the employee
        var dailyPay = Position.HourlyPay * Position.ShiftLength;
        Character.Money += dailyPay;
            
        // Apply work effects
        Character.Needs[NeedType.Energy].Modify(-30f);
        Character.Needs[NeedType.Fun].Modify(-20f);
            
        // Improve skills
        Character.ImproveSkill("Customer Service", 0.1f * Performance);
        Character.ImproveSkill("Sales", 0.1f * Performance);
    }

    private float CalculateDailyPerformance()
    {
        var salesScore = Math.Min(1.0f, (SalesTotal / (1000f * Position.Level)));
        var customerScore = Math.Min(1.0f, CustomersServed / (10f * Position.Level));
        return (salesScore + customerScore) / 2f;
    }

    public void ServeCostumer(float saleAmount)
    {
        CustomersServed++;
        SalesTotal += saleAmount;
    }
}