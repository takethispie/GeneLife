using GeneLife.Core.Planning.Objective;

namespace GeneLife.Core.Planning;

public static class PlannerBuilder
{
    public static IPlannerSlot[] BasicDay() => [
        new EmptySlot(9, 1),
        new EmptySlot(10, 1),
        new EmptySlot(11, 1),
        new Eat(10, new TimeOnly(12, 0, 0), 1),
        new EmptySlot(13, 1),
        new EmptySlot(14, 1),
        new EmptySlot(15, 1),
        new EmptySlot(16, 1),
        new EmptySlot(17, 1),
        new EmptySlot(18, 1),
        new EmptySlot(19, 1),
        new Eat(10, new TimeOnly(20, 0, 0), 1),
        new EmptySlot(21, 1),
        new Sleep(10, new TimeOnly(22, 0, 0), 2),
        new Sleep(10, new TimeOnly(0, 0, 0), 9),
    ];
}
