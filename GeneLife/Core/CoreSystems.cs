using Arch.Core;
using Arch.System;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Systems;

namespace GeneLife.Core;

public class CoreSystem
{
    public static void Register(World world, Group<float> group, ArchetypeFactory archetypeFactory)
    {
        group.Add(new ShopSystem(world, archetypeFactory));
        group.Add(new MoveSystem(world, archetypeFactory));
        group.Add(new GlobalSystem(world));
    }
}