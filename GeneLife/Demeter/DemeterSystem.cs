using Arch.Core;
using Arch.System;
using GeneLife.Core.Entities.Factories;
using GeneLife.Demeter.Systems;

namespace GeneLife.Demeter;

public class DemeterSystem
{
    public static void Register(World world, Group<float> group, ArchetypeFactory archetypeFactory)
    {
        group.Add(
            new EatingSystem(world, archetypeFactory), 
            new DrinkingSystem(world, archetypeFactory), 
            new HungerSystem(world, archetypeFactory), 
            new ThirstSystem(world, archetypeFactory)
        );
    }
}