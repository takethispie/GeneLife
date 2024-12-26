using Genelife.Domain.Human;

namespace Genelife.Domain.Items;

public static class ItemFactory
{
    public static Item CreateFood(string name, decimal value, float hungerEffect)
    {
        var food = new Item(name, ItemCategory.Food, value, isStackable: true, maxStackSize: 10, durability: 1);
        food.UseEffects[NeedType.Hunger] = hungerEffect;
        return food;
    }

    public static Item CreateFurniture(string name, decimal value, ItemQuality quality)
    {
        return new Item(name, ItemCategory.Furniture, value, quality, durability: 100);
    }

    public static Item CreateBook(string name, decimal value)
    {
        var book = new Item(name, ItemCategory.Book, value, durability: 50);
        book.UseEffects[NeedType.Fun] = 10f;
        return book;
    }
}