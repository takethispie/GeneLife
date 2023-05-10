using Arch.Core.Utils;
using GeneLife.CommonComponents;
using GeneLife.CommonComponents.Status;

namespace GeneLife.Entities;

public static class LiquidsArchetypeFactory
{
    public static ComponentType[] Water() => new ComponentType[]
    {
        typeof(Liquid),
        typeof(Ownable),
        typeof(Wet),
        typeof(Movable)
    };
    
    public static ComponentType[] Alcohol() => new ComponentType[]
    {
        typeof(Liquid),
        typeof(Ownable),
        typeof(Flammable),
        typeof(Wet),
        typeof(Movable)
    };
}