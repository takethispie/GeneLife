using Arch.Core;

namespace Genelife.Family;

/// <summary>
/// <see cref="Entity"/> is a value type, so LINQ's FirstOrDefault returns
/// default(Entity) (Id 0) rather than <see cref="Entity.Null"/>. Comparing the
/// default against Entity.Null treats "nothing found" as a real entity. These
/// helpers preserve the intended "no entity" sentinel.
/// </summary>
public static class EntityLookup
{
    public static Entity FirstOrNull(this IReadOnlyList<Entity> entities) =>
        entities.Count > 0 ? entities[0] : Entity.Null;

    public static Entity FirstOrNull(this IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
            return entity;
        return Entity.Null;
    }

    public static Entity FirstOrNull(this IEnumerable<Entity> entities, Func<Entity, bool> predicate)
    {
        foreach (var entity in entities)
            if (predicate(entity))
                return entity;
        return Entity.Null;
    }
}
