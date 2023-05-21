using GeneLife.Core.Items;

namespace GeneLife.Core.Utils;

public static class ItemExtensions
{
    public static string Description(this Item item) => $"Id: {item.Id} Type: {item.Type}";
}