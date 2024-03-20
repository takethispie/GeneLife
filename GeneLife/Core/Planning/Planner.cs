using GeneLife.Core.Objective;

namespace GeneLife.Core.Planning;

public class Planner(List<IPlannerSlot> slots)
{
    public IPlannerSlot[] Slots { get; private set; } = [.. slots];
    private IObjective[] awaitingObjectives = [];

    public void AddObjectivesToWaitingList(IObjective objective) => awaitingObjectives = [.. awaitingObjectives, objective];

    public IList<IObjective> GetWaitingObjectivesList() => awaitingObjectives;

    public IPlannerSlot GetSlot(TimeOnly time)
    {
        return Slots.FirstOrDefault(x => x.Start.Equals(time)) ?? new UnplannedTimeSlot(time.Hour, 1);
    }

    public IPlannerSlot GetNextSlot(TimeOnly time)
    {
        return Slots
            .OrderBy(x => x.Start)
            .FirstOrDefault(x => x.Start.CompareTo(time) >= 0) ?? new UnplannedTimeSlot(time.Hour, 1);
    }

    public void SetSlot(IPlannerSlot slot) => Slots = Slots.ToList().Select(x =>
    {
        if (x.Start.Equals(slot.Start)) return slot;
        return x;
    }).ToArray();

    public IPlannerSlot? GetFirstFreeSlot(TimeOnly time)
    {
        return Slots
            .Where(x => x.Start.CompareTo(time) >= 0)
            .FirstOrDefault(x => x is EmptyPlannerSlot);
    }

    public IPlannerSlot? GetFirstFreeSlot()
    {
        return Slots
            .FirstOrDefault(x => x is EmptyPlannerSlot);
    }

    public List<ObjectiveSlot> GetAllObjectivePlannerSlots() => Slots.Where(x => x is ObjectiveSlot).Cast<ObjectiveSlot>().ToList();
}
