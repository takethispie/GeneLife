using System.Linq.Expressions;
using Marten;

namespace Genelife.Api.Projections;

/// <summary>
/// Shared upsert lookup for family projections: deterministic document id first,
/// then the legacy network-id query fallback, then a fresh document.
/// </summary>
internal static class ProjectionDocumentStore
{
    public static async Task<T> GetOrCreate<T>(
        IDocumentOperations ops,
        Guid id,
        Expression<Func<T, bool>> predicate,
        Func<T> create)
        where T : class
    {
        var document = await ops.LoadAsync<T>(id);
        if (document is not null)
            return document;

        document = await ops.Query<T>()
            .Where(predicate)
            .FirstOrDefaultAsync();
        return document ?? create();
    }
}
