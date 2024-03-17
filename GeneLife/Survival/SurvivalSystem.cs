using Arch.Core;
using Arch.System;
using GeneLife.Core.Entities.Factories;
using GeneLife.Survival.Systems;

namespace GeneLife.Survival;

public class SurvivalSystem
{
    public static void Register(World world, Group<float> group, ArchetypeFactory archetypeFactory)
    {
        group.Add(new HungerSystem(world, archetypeFactory));
        group.Add(new ThirstSystem(world, archetypeFactory));
    }
}