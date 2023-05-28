using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Core.Entities.Factories;
using GeneLife.Core.Items;

namespace GeneLife.Core.Entities.Generators;

public static class ShopGenerator
{
    private static Entity ShopBluePrint(World world, Position position, Adress adress)
    {
        var newShop = world.Create(new BuildingsArchetypeFactory().Build("shop"));
        newShop.Set(position);
        newShop.Set(adress);
        newShop.Set(new Flammable(1));
        newShop.Set(new Lifespan(TimeSpan.FromDays(365*100)));
        newShop.Set(new Ownable(-1));
        return newShop;
    }
    
    public static Entity SmallGeneralShop(World world, Position position, Adress adress, IEnumerable<ItemWithPrice> items)
    {

        var newShop = ShopBluePrint(world, position, adress);
        newShop.Set(new Shop(items.Where(x => x.Price < 20).ToArray()));
        return newShop;
    }
    
    public static Entity MediumGeneralShop(World world, Position position, Adress adress, IEnumerable<ItemWithPrice> items)
    {
        
        var newShop = ShopBluePrint(world, position, adress);
        newShop.Set(new Shop(items.Where(x => x.Price < 100).ToArray()));
        return newShop;
    }
    
    public static Entity LargeGeneralShop(World world, Position position, Adress adress, IEnumerable<ItemWithPrice> items)
    {
        
        var newShop = ShopBluePrint(world, position, adress);
        newShop.Set(new Shop(items.Where(x => x.Price < 1000).ToArray()));
        return newShop;
    }
}