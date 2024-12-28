using Genelife.Domain.Shop;

namespace Genelife.Domain.Work;

public class StorePosition : Career
{
    public Guid Workplace { get; }
    public List<DayOfWeek> ShiftDays { get; private set; }
    public int ShiftStart { get; private set; }
    public int ShiftLength { get; private set; }

    public StorePosition(
        Guid workplace,
        string title,
        float startingPay,
        List<DayOfWeek> shiftDays,
        int shiftStart,
        int shiftLength) 
        : base(title, startingPay, shiftLength, shiftDays.ToArray())
    {
        Workplace = workplace;
        ShiftDays = shiftDays;
        ShiftStart = shiftStart;
        ShiftLength = shiftLength;
        
        // Override career levels for store positions
        CareerLevels.Clear();
        CareerLevels.AddRange(new List<CareerLevel>
        {
            new CareerLevel(1, "Trainee", startingPay, 
                new Dictionary<string, float> { { "Customer Service", 1 }, { "Sales", 1 } }),
            new CareerLevel(2, "Associate", startingPay * 1.2f, 
                new Dictionary<string, float> { { "Customer Service", 2 }, { "Sales", 2 } }),
            new CareerLevel(3, "Senior Associate", startingPay * 1.5f, 
                new Dictionary<string, float> { { "Customer Service", 3 }, { "Sales", 3 }, { "Leadership", 1 } }),
            new CareerLevel(4, "Supervisor", startingPay * 2.0f, 
                new Dictionary<string, float> { { "Customer Service", 4 }, { "Sales", 4 }, { "Leadership", 2 } }),
            new CareerLevel(5, "Manager", startingPay * 3.0f, 
                new Dictionary<string, float> { { "Customer Service", 5 }, { "Sales", 5 }, { "Leadership", 4 } })
        });
    }

    public bool IsScheduledToWork(DateTime currentTime)
    {
        return ShiftDays.Contains(currentTime.DayOfWeek) &&
               currentTime.TimeOfDay.Hours >= ShiftStart &&
               currentTime.TimeOfDay.Hours < ShiftStart + ShiftLength;
    }
}