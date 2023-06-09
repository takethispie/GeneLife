using Arch.Core;
using Arch.System;
using GeneLife.Athena.Systems;
using GeneLife.Core.Entities.Factories;

namespace GeneLife.Athena;

public class AthenaSystem
{
    public static void Register(World world, Group<float> group, ArchetypeFactory archetypeFactory)
    {
        group.Add(new ShopPathSystem(world, archetypeFactory));
    }
}