using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Status;
using GeneLife.Entities.Exceptions;
using GeneLife.Entities.Interfaces;

namespace GeneLife.Entities.Factories;

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