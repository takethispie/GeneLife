namespace GeneLife.Core.Extensions;

public static class CollectionExtensions
{
    public static T Random<T>(this IEnumerable<T> collection, Random random)
    {
        var list = collection.ToList();
        return list.ElementAt(random.Next(0, list.Count));
    }
}