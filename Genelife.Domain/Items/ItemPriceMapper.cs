namespace Genelife.Domain;

public static class ItemPriceMapper {
    public static int Map(ItemType itemType) => itemType switch {
        ItemType.Drink => 2,
        ItemType.Food => 5,
        _ => 0
    };
}