using GeneLife.Core.Objective;

namespace GeneLife.Core.Planning;

public static class PlannerBuilder
{
    public static Planner BasicHumanPlanning() => new(BasicDay());

    public static IPlannerSlot[] BasicDay() => [
        new EmptyPlannerSlot(9, 1),
        new EmptyPlannerSlot(10, 1),
        new EmptyPlannerSlot(11, 1),
        new ObjectiveSlot(12, 1, new Eat(10)),
        new EmptyPlannerSlot(13, 1),
        new EmptyPlannerSlot(14, 1),
        new EmptyPlannerSlot(15, 1),
        new EmptyPlannerSlot(16, 1),
        new EmptyPlannerSlot(17, 1),
        new EmptyPlannerSlot(18, 1),
        new EmptyPlannerSlot(19, 1),
        new ObjectiveSlot(20, 1, new Eat(10)),
        new ObjectiveSlot(21, 2, new Sleep(10)),
        new ObjectiveSlot(0, 9, new Sleep(10)),
    ];
}
