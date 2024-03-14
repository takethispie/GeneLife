namespace GeneLife.Core.Planning;
public class EmptyPlannerSlot(int startHour, int duration) : IPlannerSlot
{
    public TimeOnly Start { get; init; } = new TimeOnly(startHour, 0);

    public TimeSpan Duration { get; init; } = new TimeSpan(duration, 0, 0);
}
