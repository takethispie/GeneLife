using Arch.Core;
using Arch.System;
using GeneLife.Sibyl.Systems;

namespace GeneLife.Sibyl;

public static class SibylSystem
{
    public static void Register(World world, Group<float> group)
    {
        group.Add(new LearningSystem(world));
    }
}