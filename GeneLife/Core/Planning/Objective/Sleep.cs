namespace GeneLife.Core.Planning.Objective;

public class Sleep(int Priority, TimeOnly start, int duration) : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; } = start;
    public TimeSpan Duration { get; init; } = TimeSpan.FromHours(duration);
    public int Priority { get; set; } = Priority;
    public string Name { get; init; } = GetName();

    public static string GetName() => "Sleep";
}