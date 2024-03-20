using GeneLife.Core.Objective;

namespace GeneLife.Core.Planning;
public class ObjectiveSlot(int startHour, int duration, IObjective objective) : IPlannerSlot
{
    public TimeOnly Start { get; init; } = new TimeOnly(startHour, 0);

    public TimeSpan Duration { get; init; } = new TimeSpan(duration, 0, 0);
    public IObjective Objective { get; init; } = objective;
}
