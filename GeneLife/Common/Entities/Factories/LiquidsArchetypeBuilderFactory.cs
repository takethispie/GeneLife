using Arch.Core.Utils;
using GeneLife.Common.Components;
using GeneLife.Common.Components.Status;
using GeneLife.Common.Entities.Exceptions;
using GeneLife.Common.Entities.Interfaces;

namespace GeneLife.Common.Entities.Factories;

public class LiquidsArchetypeBuilderFactory : IArchetypeBuilder
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