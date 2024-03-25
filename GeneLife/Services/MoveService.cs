﻿using GeneLife.Core.Data;
using System.Numerics;

namespace GeneLife.Services;
internal static class MoveService
{
    public static bool IsReachable(Vector3 current, Vector3 target, float speed, int durationHours)
    {
        var distance = Vector3.DistanceSquared(current, target);
        var availableTicks = Math.Round(Constants.TicksPerDay / 24f) * durationHours;
        return distance <= availableTicks * speed;
    }

    public static int TimeToReach(Vector3 current, Vector3 target, float speed)
    {
        var distance = Vector3.DistanceSquared(current, target);
        var ticks = Math.Round(distance / speed);
        return Convert.ToInt32(Math.Round(ticks / Math.Round(Constants.TicksPerDay / 24f)));
    }
}
