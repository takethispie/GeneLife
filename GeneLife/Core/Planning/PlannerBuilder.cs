using GeneLife.Core.Planning.Objective;

namespace GeneLife.Core.Planning;

public static class PlannerBuilder
{
    public static Planner BasicHumanPlanning() => new(BasicDay());

    public static IPlannerSlot[] BasicDay() => [
        new EmptyPlannerSlot(9, 1),
        new EmptyPlannerSlot(10, 1),
        new EmptyPlannerSlot(11, 1),
        new Eat(10, new TimeOnly(12, 0, 0), 1),
        new EmptyPlannerSlot(13, 1),
        new EmptyPlannerSlot(14, 1),
        new EmptyPlannerSlot(15, 1),
        new EmptyPlannerSlot(16, 1),
        new EmptyPlannerSlot(17, 1),
        new EmptyPlannerSlot(18, 1),
        new EmptyPlannerSlot(19, 1),
        new Eat(10, new TimeOnly(20, 0, 0), 1),
        new EmptyPlannerSlot(21, 1),
        new Sleep(10, new TimeOnly(22, 0, 0), 2),
        new Sleep(10, new TimeOnly(0, 0, 0), 9),
    ];
}
