using Arch.Core;
using Arch.System;
using GeneLife.Oracle.Systems;
using GeneLife.Sibyl.Systems;

namespace GeneLife.Oracle;

public static class OracleSystem
{
    public static void Register(World world, Group<float> group)
    {
        group.Add(new LoveInterestSystem(world));
    }
}