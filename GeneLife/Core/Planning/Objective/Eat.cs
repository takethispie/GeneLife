namespace GeneLife.Core.Planning.Objective;

public struct Eat : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; }
    public TimeSpan Duration { get; init; }
    public int Priority { get; set; }
    public string Name { get; init; }

    public Eat(int Priority, TimeOnly start, int duration, string Name = "Eat")
    {
        Start = start;
        Duration = TimeSpan.FromHours(duration);
        this.Priority = Priority;
        this.Name = Name;
    }
}