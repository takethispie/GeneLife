﻿using System.Numerics;

namespace GeneLife.Core.Planning.Objective;

public struct MoveTo : IObjective, IPlannerSlot
{
    public TimeOnly Start { get; init; }
    public TimeSpan Duration { get; init; }
    public int Priority { get; set; }
    public Vector3 Target { get; init; }
    public string Name { get; init; }

    public MoveTo(int priority, Vector3 target, TimeOnly start, int duration)
    {
        Priority = priority;
        Target = target;
        Name = GetName();
        Start = start;
        Duration = TimeSpan.FromHours(duration);
    }

    public static string GetName() => "MoveTo";
}