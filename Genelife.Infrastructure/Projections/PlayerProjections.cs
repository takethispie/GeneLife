using Genelife.Api.Models;
using Genelife.Messages.Events;
using Marten;
using Marten.Events.Projections;

namespace Genelife.Api.Projections;

/// <summary>
/// Player-life audit projection. Handlers are idempotent upserts so replaying
/// the event stream produces the same document.
/// </summary>
public sealed partial class PlayerProjection : EventProjection
{
    public async Task Project(PlayerProfileCreated e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PlayerId);
        doc.GenosNetworkId = e.GenosNetworkId;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerHired e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, await PlayerIdForGenos(ops, e.GenosNetworkId));
        doc.Employed = true;
        doc.CompanyNetworkId = e.CompanyNetworkId;
        doc.OfficeNetworkId = e.OfficeNetworkId;
        doc.Position = e.Position;
        doc.SalaryCents = e.SalaryCents;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerQuitJob e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, await PlayerIdForGenos(ops, e.GenosNetworkId));
        doc.Employed = false;
        doc.Position = string.Empty;
        doc.SalaryCents = 0;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerTerminated e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, await PlayerIdForGenos(ops, e.GenosNetworkId));
        doc.Employed = false;
        doc.Position = string.Empty;
        doc.SalaryCents = 0;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerLeaseStarted e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, await PlayerIdForGenos(ops, e.GenosNetworkId));
        doc.Housed = true;
        doc.HouseholdNetworkId = e.HouseholdNetworkId;
        doc.UnitNetworkId = e.UnitNetworkId;
        doc.RentCents = e.RentCents;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerMoved e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, await PlayerIdForGenos(ops, e.GenosNetworkId));
        doc.HouseholdNetworkId = e.HouseholdNetworkId;
        doc.UnitNetworkId = e.ToUnitNetworkId;
        doc.Housed = e.ToUnitNetworkId != 0;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerAwaitingContinuation e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PlayerId);
        doc.AwaitingContinuation = true;
        doc.Employed = false;
        doc.Housed = false;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerContinuationApplied e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PlayerId);
        doc.AwaitingContinuation = false;
        doc.Observer = e.Policy == "observer";
        doc.GenosNetworkId = e.NewGenosNetworkId;
        doc.Employed = false;
        doc.Housed = false;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerCommandApplied e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PlayerId);
        doc.GenosNetworkId = e.GenosNetworkId;
        doc.LastCommandId = e.CommandId;
        doc.LastCommandKind = e.Kind;
        doc.LastCommandStatus = e.Status;
        doc.LastCommandCode = e.Code;
        doc.LastCommandTick = e.Tick;
        if (string.Equals(e.Status, "Applied", StringComparison.Ordinal))
            doc.CommandsApplied++;
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    public async Task Project(PlayerAdminActionApplied e, IDocumentOperations ops)
    {
        var doc = await GetOrCreate(ops, e.PlayerId);
        if (string.Equals(e.Action, "set-continuation", StringComparison.Ordinal))
        {
            var policy = e.Reason.Contains("Observer", StringComparison.OrdinalIgnoreCase)
                ? "Observer"
                : doc.ContinuationPolicy;
            doc.ContinuationPolicy = policy;
        }
        doc.UpdatedTick = e.Tick;
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        ops.Store(doc);
    }

    private static async Task<PlayerDocument> GetOrCreate(IDocumentOperations ops, string playerId)
    {
        var doc = await ops.LoadAsync<PlayerDocument>(playerId);
        if (doc is null)
        {
            doc = new PlayerDocument { Id = playerId };
        }
        return doc;
    }

    private static async Task<string> PlayerIdForGenos(IDocumentOperations ops, ulong genosNetworkId)
    {
        var players = await ops.Query<PlayerDocument>()
            .Where(doc => doc.GenosNetworkId == genosNetworkId)
            .ToListAsync();
        return players.Count > 0 ? players[^1].Id : $"genos-{genosNetworkId}";
    }
}
