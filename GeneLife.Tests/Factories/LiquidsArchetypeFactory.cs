using Arch.Core.Utils;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Status;
using GeneLife.Core.Entities.Exceptions;
using GeneLife.Core.Entities.Interfaces;

namespace GeneLife.Tests.Factories;

internal class LiquidsArchetypeFactory : IArchetypeBuilder
{
    private static ComponentType[] Water() => new ComponentType[]
    {
        typeof(Liquid),
        typeof(Ownable),
        typeof(Wet),
        typeof(Moving)
    };

    private static ComponentType[] Alcohol() => new ComponentType[]
    {
        typeof(Liquid),
        typeof(Ownable),
        typeof(Flammable),
        typeof(Wet),
        typeof(Moving)
    };

    public ComponentType[] Build(string type) => type.ToLower() switch
    {
        "water" => Water(),
        "alcohol" => Alcohol(),
        _ => throw new ArchetypeNotFoundException()
    };

    public string[] ArchetypesList() => new[] { "Water", "Alcohol" };
}