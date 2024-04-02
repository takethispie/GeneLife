﻿using GeneLife.Core.Planning.Objective;

namespace GeneLife.Core.Planning;

public class Planner()
{
    public IPlannerSlot[] Slots { get; private set; } = PlannerBuilder.BasicDay();
    private IObjective[] awaitingObjectives = [];

    public void AddObjectivesToWaitingList(IObjective objective) => awaitingObjectives = [.. awaitingObjectives, objective];

    public IList<IObjective> GetWaitingObjectivesList() => awaitingObjectives;

    public IPlannerSlot? GetSlot(TimeOnly time)
    {
        return Slots.FirstOrDefault(x => x.Start.Equals(time));
    }

    public IPlannerSlot? GetSlot(DateTime time) => GetSlot(TimeOnly.FromDateTime(time));

    public IPlannerSlot? GetNextSlot(TimeOnly time)
    {
        return Slots
            .OrderBy(x => x.Start)
            .FirstOrDefault(x => x.Start.CompareTo(time) >= 0);
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
            .FirstOrDefault(x => x is EmptySlot);
    }

    public IPlannerSlot? GetFirstFreeSlot(DateTime time) => GetFirstFreeSlot(TimeOnly.FromDateTime(time));

    public IPlannerSlot? GetFirstFreeSlot()
    {
        return Slots
            .FirstOrDefault(x => x is EmptySlot);
    }

    public List<IPlannerSlot> GetAllFilledPlannerSlots() => Slots.Where(x => x is not EmptySlot).ToList();

    public bool SetFirstFreeSlot(IPlannerSlot slot)
    {
        var res = false;
        Slots = Slots.Select(x => {
            if (x.Start.CompareTo(slot.Start) == 0 && x.Duration == slot.Duration && x is EmptySlot)
            {
                res = true;
                return slot;
            }
            return x;
        }).ToArray();
        return res;
    }

    public string[] ToStrings() => 
        Slots.Select(sl => sl.Start.Hour.ToString().PadLeft(2, '0') 
            + ":00 " 
            + sl.Duration.Hours.ToString()
            + "h - " 
            + sl.Name
        ).ToArray();
}
