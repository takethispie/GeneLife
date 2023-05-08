using Arch.Core.Utils;
using GeneLife.Components;
using GeneLife.Environment;
using GeneLife.Genetic;

namespace GeneLife.Entities;

public static class ArchetypeFactory
{
    public static ComponentType[] Person() => new ComponentType[] { typeof(Identity), typeof(Genome), typeof(EnvironmentalTraits) };
}