namespace GeneLife.Core.Planning.Objective;

public struct Drink : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; }
    public TimeSpan Duration { get; init; }
    public int Priority { get; set; }
    public string Name { get; init; }

    public Drink(int Priority)
    {
        this.Priority = Priority;
        this.Name = GetName();
    }

    public static string GetName() => "Drink";
}