namespace GeneLife.Core.Utils;

public static class CollectionExtensions
{
    public static T Random<T>(this IEnumerable<T> collection, Random random)
    {
        var enumerable = collection.ToList();
        return enumerable.ElementAt(random.Next(0, enumerable.Count));
    }
}