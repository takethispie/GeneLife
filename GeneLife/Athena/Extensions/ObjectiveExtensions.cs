using GeneLife.Athena.Core.Objectives;

namespace GeneLife.Athena.Extensions;

public static class ObjectiveExtensions
{
    public static bool IsHighestPriority(this IEnumerable<IObjective> currentObjectives, Type objectiveType)
    {
        var top = currentObjectives.MaxBy(x => x.Priority);
        return top != null && top.GetType() == objectiveType;
    }
}