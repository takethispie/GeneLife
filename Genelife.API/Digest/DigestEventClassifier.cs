using System.Collections.Concurrent;
using System.Reflection;
using Genelife.Messages.Events;

namespace Genelife.Api.Digest;

/// <summary>
/// Reduces a domain event to its digest coordinates
/// <c>(EventKind, SubjectEntityType, SubjectEntityId, Tick)</c>.
///
/// The subject is chosen from the event's own fields by an ordered, server-owned
/// rule — the client never derives it. Events with no subject (pure aggregates,
/// batch totals) are not digestible and are dropped.
///
/// Suppressed by design (GDD ch.08 section 8.5(6)):
/// <list type="bullet">
/// <item>ambient per-tick noise (<see cref="GenosNeedsChanged"/>) — the digest
/// carries state changes the player did not initiate, not the breath of the
/// world;</item>
/// <item>aggregate and metrics snapshots, which belong to the operator plane
/// (section 8.6) rather than to a player's digest;</item>
/// <item>ledger rows and admin-action audit rows, which already have their own
/// surfaces.</item>
/// </list>
/// </summary>
public static class DigestEventClassifier
{
    /// <summary>Event types the digest never carries.</summary>
    private static readonly HashSet<Type> Suppressed =
    [
        typeof(GenosNeedsChanged),
        typeof(TickCompleted),
        typeof(LedgerEntryRecorded),
        typeof(EconomySnapshotTaken),
        typeof(HousingSnapshotTaken),
        typeof(ConsumptionMetricsRecorded),
        typeof(PlayerLifeMetricsRecorded),
        typeof(UtilityNetworkStateChanged),
        typeof(HousingMarketUpdated),
        typeof(PopulationSpawned),
        typeof(PopulationGenerated),
        typeof(PopulationHealthCheck),
        typeof(ImmigrationPaused),
        typeof(ArrivalCancelled),
        typeof(HousingMigrated),
        typeof(BasicIncomeBatchPaid),
        typeof(ConsumerSpendingBatchCompleted),
        typeof(UnmetDemandRecorded),
        typeof(ConsumptionAdminActionApplied),
        typeof(FamilyAdminActionApplied),
        typeof(PlayerAdminActionApplied),
        typeof(MapGenerated),
        typeof(BuildingPlaced),
        typeof(BuildingRemoved),
        typeof(BuildingRelocated),
    ];

    /// <summary>
    /// Events whose only identifier is a bare <c>NetworkId</c>; the entity type
    /// is fixed per event so the ambiguous name never leaks into the grouping.
    /// </summary>
    private static readonly Dictionary<Type, string> BareNetworkIdSubject = new()
    {
        [typeof(GenosCreated)] = "genos",
        [typeof(GenosDeleted)] = "genos",
        [typeof(GenosActionStarted)] = "genos",
        [typeof(GenosActionCompleted)] = "genos",
        [typeof(CompanyCreated)] = "company",
        [typeof(CompanyDeleted)] = "company",
    };

    /// <summary>
    /// Ordered <c>property → entity type</c> rules. The first non-zero match on
    /// an event wins, so the ordering encodes the subject precedence (a genos is
    /// more specific than the store it shops at).
    /// </summary>
    private static readonly (string Property, string EntityType)[] SubjectProperties =
    [
        ("GenosNetworkId", "genos"),
        ("GenosId", "genos"),
        ("FromGenosId", "genos"),
        ("ToGenosId", "genos"),
        ("GenosAId", "genos"),
        ("GenosBId", "genos"),
        ("ChildNetworkId", "genos"),
        ("ParentANetworkId", "genos"),
        ("ParentBNetworkId", "genos"),
        ("CarryingParentNetworkId", "genos"),
        ("OtherParentNetworkId", "genos"),
        ("BeneficiaryNetworkId", "genos"),
        ("GuardianNetworkId", "genos"),
        ("DeceasedNetworkId", "genos"),
        ("HeadId", "genos"),
        ("SourceGenosNetworkId", "genos"),
        ("VenueNetworkId", "venue"),
        ("HouseholdNetworkId", "household"),
        ("HouseholdId", "household"),
        ("CompanyNetworkId", "company"),
        ("CompanyId", "company"),
        ("EmployerNetworkId", "company"),
        ("OfficeNetworkId", "office"),
        ("OfficeId", "office"),
        ("StoreNetworkId", "store"),
        ("PropertyId", "property"),
        ("UnitId", "unit"),
        ("UnitNetworkId", "unit"),
    ];

    private static readonly ConcurrentDictionary<Type, Func<object, long, DigestEvent?>> Classifiers = new();

    /// <summary>
    /// Classifies an event. Returns <c>false</c> when the event is not part of
    /// the digest (suppressed or subject-less). <paramref name="fallbackTick"/>
    /// is used for events that do not carry a tick of their own.
    /// </summary>
    public static bool TryClassify(object @event, long fallbackTick, out DigestEvent digest)
    {
        var result = Classifiers.GetOrAdd(@event.GetType(), Build)(@event, fallbackTick);
        if (result is { } value)
        {
            digest = value;
            return true;
        }

        digest = default;
        return false;
    }

    private static Func<object, long, DigestEvent?> Build(Type type)
    {
        if (Suppressed.Contains(type))
            return static (_, _) => null;

        var kind = type.Name;
        var tickProperty = type.GetProperty("Tick")?.GetMethod?.ReturnType == typeof(long)
            ? type.GetProperty("Tick")
            : null;

        if (BareNetworkIdSubject.TryGetValue(type, out var bareEntityType))
        {
            var networkId = type.GetProperty("NetworkId");
            return (e, fallback) => Read(e, networkId, bareEntityType, kind, tickProperty, fallback);
        }

        var resolved = new List<(PropertyInfo Property, string EntityType)>();
        foreach (var (propertyName, entityType) in SubjectProperties)
        {
            var property = type.GetProperty(propertyName);
            if (property is not null && property.GetMethod?.ReturnType == typeof(ulong))
                resolved.Add((property, entityType));
        }

        if (resolved.Count == 0)
            return static (_, _) => null;

        var rules = resolved.ToArray();
        return (e, fallback) =>
        {
            foreach (var (property, entityType) in rules)
            {
                var value = Read(e, property, entityType, kind, tickProperty, fallback);
                if (value is { } digest)
                    return digest;
            }

            return null;
        };
    }

    private static DigestEvent? Read(
        object @event,
        PropertyInfo? subjectProperty,
        string entityType,
        string kind,
        PropertyInfo? tickProperty,
        long fallbackTick)
    {
        if (subjectProperty?.GetValue(@event) is not ulong id || id == 0)
            return null;

        var tick = tickProperty?.GetValue(@event) is long value ? value : fallbackTick;
        return new DigestEvent(kind, entityType, id, tick);
    }
}
