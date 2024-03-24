namespace GeneLife.Core.Planning.Objective;

public struct Drink : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; }
    public TimeSpan Duration { get; init; }
    public int Priority { get; set; }
    public string Name { get; init; }

    public Drink(int Priority, string Name = "Drink")
    {
        this.Priority = Priority;
        this.Name = Name;
    }
}