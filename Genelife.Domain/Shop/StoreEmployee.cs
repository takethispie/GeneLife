using Genelife.Domain.Human;
using Genelife.Domain.Work;

namespace Genelife.Domain.Shop;

public class StoreEmployee(Character character, StorePosition position) {
    public Character Character { get; } = character;
    public StorePosition Position { get; private set; } = position;
    public DateTime LastShiftDate { get; private set; }
    public int CustomersServed { get; private set; }
    public float SalesTotal { get; private set; }
    public float Performance { get; private set; } = 0.75f;
    public bool ShiftStarted { get; private set; }
    public DateTime ShiftStartedDate { get; private set; }

    public void StartShift(DateTime currentTime) {
        if (!Position.IsScheduledToWork(currentTime) || !ShiftStarted) return;
        ShiftStarted = true;
        ShiftStartedDate = currentTime;
        CustomersServed = 0;
        SalesTotal = 0;
    }

    public void EndShift(DateTime currentTime) {
        if (!ShiftStarted 
            || currentTime.TimeOfDay < LastShiftDate.TimeOfDay.Add(
                new TimeSpan(0, Position.ShiftLength, 0, 0))) return;
        LastShiftDate = currentTime.Date;
        var dailyPerformance = CalculateDailyPerformance();
        Performance = (Performance * 0.7f) + (dailyPerformance * 0.3f);
        var dailyPay = Position.HourlyPay * Position.ShiftLength;
        Character.Money += dailyPay;
        Character.Needs[NeedType.Energy].Modify(-30f);
        Character.Needs[NeedType.Fun].Modify(-20f);
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