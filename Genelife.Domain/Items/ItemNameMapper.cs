namespace Genelife.Domain;

public static class ItemMapper {
    public static Item? Map(int Id) => Id switch {
        0 => new Item(Id, "Water", ItemType.Drink),
        1 => new Item(Id, "Food", ItemType.Food),
        _ => null
    };

    public static Item? Map(ItemType itemType) => itemType switch {
        ItemType.Drink => new Item(0, "Water", ItemType.Drink),
        ItemType.Food => new Item(1, "Food", ItemType.Food),
        _ => null
    };
}