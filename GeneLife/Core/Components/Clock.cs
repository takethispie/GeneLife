namespace GeneLife.Core.Components;
public struct Clock
{
    public int Day;
    public DayOfWeek DayOfWeek;
    public float ticks;

    public Clock()
    {
        Day = 0;
        ticks = 0;
        DayOfWeek = DayOfWeek.Monday;
    }
}
