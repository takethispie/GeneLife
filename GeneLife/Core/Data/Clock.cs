namespace GeneLife.Core.Data;
public static class Clock
{
    public static DateTime Time {  get; set; }
    public static int Tick { get; set; } = 0;

    public static void RunClock()
    {
        Tick = Constants.TicksPerDay switch
        {
            24 when Tick is >= 1 => AddHour(),
            48 when Tick is >= 2 => AddHour(),
            192 when Tick is >= 8 => AddHour(),
            240 when Tick is >= 10 => AddHour(),
            _ => Tick + 1
        };
    }

    private static int AddHour()
    {
        Time = Time.AddHours(1);
        return 0;
    }

    public static int ScaleDuration(int durationInTicks)
    {
        return Constants.TicksPerDay switch
        {
            24 => 1,
            48 => Convert.ToInt32(Math.Ceiling(Convert.ToDecimal(durationInTicks) / 2)),
            192 => Convert.ToInt32(Math.Ceiling(Convert.ToDecimal(durationInTicks) / 8)),
            _ => durationInTicks
        };
    }
}
