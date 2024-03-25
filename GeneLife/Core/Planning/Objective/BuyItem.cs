namespace GeneLife.Core.Planning.Objective;

public struct BuyItem(int priority, int itemId, TimeOnly Start, int duration) : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; } = Start;
    public TimeSpan Duration { get; init; } = TimeSpan.FromHours(duration);

    public int Priority { get; set; } = priority;
    public int ItemId { get; init; } = itemId;
    public string Name { get; init; } = GetName();

    public static string GetName() => "BuyItem";
}