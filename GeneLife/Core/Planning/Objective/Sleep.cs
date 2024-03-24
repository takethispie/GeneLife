namespace GeneLife.Core.Planning.Objective;

public class Sleep : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; }
    public TimeSpan Duration { get; init; }
    public int Priority { get; set; }
    public string Name { get; init; }

    public Sleep(int Priority, TimeOnly start, int duration, string Name = "Sleep")
    {
        Start = start;
        Duration = TimeSpan.FromHours(duration);
        this.Priority = Priority;
        this.Name = Name;
    }
}