namespace GeneLife.Core.Planning;

public interface IPlannerSlot
{
    public TimeOnly Start { get; }
    public TimeSpan Duration { get; }
}
