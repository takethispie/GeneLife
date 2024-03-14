using GeneLife.Core.Objective;

namespace GeneLife.Core.Planning;

public class Planner(IList<IPlannerSlot> slots)
{
    public IList<IPlannerSlot> Slots { get; private set; } = slots;
    private readonly List<IObjective> awaitingObjectives = [];

    public void AddObjectivesToWaitingList(IObjective objective) => awaitingObjectives.Add(objective);

    public IList<IObjective> GetWaitingObjectivesList() => awaitingObjectives;

    public IPlannerSlot GetSlot(DateTime time)
    {
        return Slots.FirstOrDefault(x => x.Equals(time)) ?? new UnplannedTimeSlot(time.Hour, 1);
    }

    public void SetSlot(DateTime startHour, IPlannerSlot slot) => Slots = Slots.Select(x =>
    {
        if (x.Start.Equals(startHour)) return slot;
        return x;
    }).ToList();
}
