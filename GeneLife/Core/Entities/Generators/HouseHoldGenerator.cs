using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Entities.Factories;

namespace GeneLife.Core.Entities.Generators;

public static class HouseHoldGenerator
{
    public static Entity House(World world, Position position, Adress adress)
    {
        var entity = world.Create(new BuildingsArchetypeFactory().Build("house"));
        entity.Set(position);
        entity.Set(adress);
        entity.Set(new Flammable(1));
        entity.Set(new Ownable(-1));
        entity.Set(new HouseHold());
        return entity;
    }
}