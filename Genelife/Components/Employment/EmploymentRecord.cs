namespace Genelife.Components.Employment;

/// <summary>
/// Rolling work performance attached to every employee. Monthly counters are
/// reset by <see cref="Systems.AttendanceSystem"/> after each review.
/// </summary>
public record struct EmploymentRecord
{
    public float Attendance;
    public float Performance;
    public int TenureCycles;
    public int Warnings;
    public long LastReviewTick;
    public long HiredAtTick;
    public int ShiftsCompleted;
    public int ShiftsExpected;
    public int Interruptions;
    public int ConsecutiveLowMonths;
    public int HighPerformanceMonths;

    public EmploymentRecord(long hiredAtTick)
    {
        Attendance = 1f;
        Performance = 0.5f;
        TenureCycles = 0;
        Warnings = 0;
        LastReviewTick = 0;
        HiredAtTick = hiredAtTick;
        ShiftsCompleted = 0;
        ShiftsExpected = 0;
        Interruptions = 0;
        ConsecutiveLowMonths = 0;
        HighPerformanceMonths = 0;
    }

    public void ResetMonthCounters()
    {
        ShiftsCompleted = 0;
        ShiftsExpected = 0;
        Interruptions = 0;
    }
}
