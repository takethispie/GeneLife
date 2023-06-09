﻿using GeneLife.Athena.Core.Objectives;

namespace GeneLife.Athena.Extensions;

public static class ObjectiveExtensions
{
    public static bool IsHighestPriority(this IEnumerable<IObjective> currentObjectives, Type objectiveType)
    {
        if(currentObjectives == null) return false;
        var top = currentObjectives.MaxBy(x => x.Priority);
        return top != null && top.GetType() == objectiveType;
    }

    public static IEnumerable<IObjective> SetNewHighestPriority(this IEnumerable<IObjective> objectives,
        IObjective objective)
    {
        return objectives.Select(x =>
        {
            if (x.Priority == 10) x.Priority = 9;
            return x;
        }).Prepend(objective);
    }
}