using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

/// <summary>
/// Inline map projection. Building placement is upserted by network id so a
/// replay or a repeated event cannot create duplicate rows.
/// </summary>
public partial class MapProjection : EventProjection
{
    public async Task Project(MapGenerated e, IDocumentOperations ops)
    {
        var existing = await ops.LoadAsync<MapDocument>(e.MapId);
        if (existing is null)
        {
            var minX = -(e.Width / 2);
            var minY = -(e.Height / 2);
            ops.Store(new MapDocument
            {
                Id = e.MapId,
                Version = e.Version,
                Seed = e.Seed,
                Width = e.Width,
                Height = e.Height,
                TileSize = e.TileSize,
                MinX = minX,
                MinY = minY,
                MaxX = minX + e.Width - 1,
                MaxY = minY + e.Height - 1,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        existing.Version = e.Version;
        existing.Seed = e.Seed;
        existing.Width = e.Width;
        existing.Height = e.Height;
        existing.TileSize = e.TileSize;
        existing.MinX = -(e.Width / 2);
        existing.MinY = -(e.Height / 2);
        existing.MaxX = existing.MinX + e.Width - 1;
        existing.MaxY = existing.MinY + e.Height - 1;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(existing);
    }

    public async Task Project(BuildingPlaced e, IDocumentOperations ops)
    {
        var existing = await FindByNetworkId(ops, e.NetworkId);
        var doc = existing ?? new BuildingDocument
        {
            Id = DocumentIds.ForNetworkId(e.NetworkId),
            NetworkId = (long)e.NetworkId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        doc.Kind = e.Kind;
        doc.Name = e.Name;
        doc.District = e.District;
        doc.X = e.X;
        doc.Y = e.Y;
        doc.Width = e.Width;
        doc.Height = e.Height;
        doc.EntryX = e.EntryX;
        doc.EntryY = e.EntryY;
        doc.MapVersion = e.MapVersion;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(BuildingRelocated e, IDocumentOperations ops)
    {
        var existing = await FindByNetworkId(ops, e.NetworkId);
        if (existing is null)
            return;

        existing.X = e.X;
        existing.Y = e.Y;
        existing.Width = e.Width;
        existing.Height = e.Height;
        existing.EntryX = e.EntryX;
        existing.EntryY = e.EntryY;
        existing.MapVersion = e.MapVersion;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(existing);
    }

    public async Task Project(BuildingRemoved e, IDocumentOperations ops)
    {
        var existing = await FindByNetworkId(ops, e.NetworkId);
        if (existing is not null)
            ops.Delete<BuildingDocument>(existing.Id);
    }

    private static Task<BuildingDocument?> FindByNetworkId(IDocumentOperations ops, ulong networkId) =>
        ops.Query<BuildingDocument>().FirstOrDefaultAsync(x => x.NetworkId == (long)networkId);
}
