using Arch.Core;
using Arch.System;
using GeneLife.Athena.Systems;
using GeneLife.Core.Entities.Factories;

namespace GeneLife.Athena;

public class AthenaSystem
{
    public static void Register(World world, Group<float> group, ArchetypeFactory archetypeFactory)
    {
        group.Add(new HungerSystem(world, archetypeFactory));
        group.Add(new ThirstSystem(world, archetypeFactory));
        group.Add(new ShopSystem(world, archetypeFactory));
        group.Add(new MoveSystem(world, archetypeFactory));
        group.Add(new ObjectivesSystem(world, archetypeFactory));
    }
}