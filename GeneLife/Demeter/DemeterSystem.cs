using Arch.Core;
using Arch.System;
using GeneLife.Demeter.Systems;

namespace GeneLife.Demeter;

public class DemeterSystem
{
    public static void Register(World world, Group<float> group)
    {
        group.Add(new EatingSystem(world), new HungerSystem(world));
    }
}