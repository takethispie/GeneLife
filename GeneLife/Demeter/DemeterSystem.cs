using Arch.Core;
using Arch.System;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Items;
using GeneLife.Demeter.Systems;

namespace GeneLife.Demeter;

public class DemeterSystem
{
    public static void Register(World world, Group<float> group, ArchetypeFactory archetypeFactory, ItemWithPrice[] items)
    {
        group.Add(
            new HungerSystem(world, archetypeFactory), 
            new ThirstSystem(world, archetypeFactory),
            new GroceriesInventorySystem(world, archetypeFactory, items)
        );
    }
}