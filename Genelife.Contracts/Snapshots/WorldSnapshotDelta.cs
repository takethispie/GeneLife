using Genelife.Api.DTOs;

namespace Genelife.Api.Snapshots;

/// <summary>
/// A per-client snapshot update. Either a full snapshot (<see cref="Full"/> is
/// true and <see cref="SinceTick"/> is 0) or a delta relative to
/// <see cref="SinceTick"/>. Removed ids let the client drop entities that no
/// longer exist without waiting for a full snapshot.
/// </summary>
public sealed class WorldSnapshotDelta
{
    public long Tick { get; init; }
    public long SinceTick { get; init; }
    public int Version { get; init; }
    public bool Full { get; init; }
    public List<GenosStatus> Genoses { get; init; } = new();
    public List<CompanyStatus> Companies { get; init; } = new();
    public List<ulong> RemovedGenoses { get; init; } = new();
    public List<ulong> RemovedCompanies { get; init; } = new();

    public static WorldSnapshotDelta FromSnapshot(WorldSnapshot snapshot) => new()
    {
        Tick = snapshot.Tick,
        SinceTick = 0,
        Version = snapshot.Version,
        Full = true,
        Genoses = snapshot.Genoses,
        Companies = snapshot.Companies
    };

    public static WorldSnapshotDelta Between(WorldSnapshot from, WorldSnapshot to)
    {
        var previousGenoses = IndexById(from.Genoses, genos => genos.NetworkId);
        var currentGenoses = IndexById(to.Genoses, genos => genos.NetworkId);
        var previousCompanies = IndexById(from.Companies, company => company.NetworkId);
        var currentCompanies = IndexById(to.Companies, company => company.NetworkId);

        var genoses = new List<GenosStatus>();
        foreach (var genos in to.Genoses)
        {
            if (!previousGenoses.TryGetValue(genos.NetworkId, out var prior) || !GenosesEqual(prior, genos))
                genoses.Add(genos);
        }

        var companies = new List<CompanyStatus>();
        foreach (var company in to.Companies)
        {
            if (!previousCompanies.TryGetValue(company.NetworkId, out var prior) || !CompaniesEqual(prior, company))
                companies.Add(company);
        }

        var removedGenoses = new List<ulong>();
        foreach (var id in previousGenoses.Keys)
        {
            if (!currentGenoses.ContainsKey(id))
                removedGenoses.Add(id);
        }

        var removedCompanies = new List<ulong>();
        foreach (var id in previousCompanies.Keys)
        {
            if (!currentCompanies.ContainsKey(id))
                removedCompanies.Add(id);
        }

        return new WorldSnapshotDelta
        {
            Tick = to.Tick,
            SinceTick = from.Tick,
            Version = to.Version,
            Full = false,
            Genoses = genoses,
            Companies = companies,
            RemovedGenoses = removedGenoses,
            RemovedCompanies = removedCompanies
        };
    }

    /// <summary>
    /// Full structural comparison of every <see cref="GenosStatus"/> field. A field
    /// omitted here is silently never replicated, so
    /// GenosStatusEqualityTests reflects over the DTO and fails if one is missed.
    /// </summary>
    public static bool GenosesEqual(GenosStatus a, GenosStatus b) =>
        a.NetworkId == b.NetworkId
        && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
        && a.Age == b.Age
        && a.IsPlayerControlled == b.IsPlayerControlled
        && a.Hunger.Equals(b.Hunger)
        && a.Energy.Equals(b.Energy)
        && a.Hygiene.Equals(b.Hygiene)
        && a.Bladder.Equals(b.Bladder)
        && string.Equals(a.CurrentAction, b.CurrentAction, StringComparison.Ordinal)
        && a.TimeRemaining.Equals(b.TimeRemaining)
        && a.X.Equals(b.X)
        && a.Y.Equals(b.Y)
        && a.TileX == b.TileX
        && a.TileY == b.TileY
        && a.HasMoveTarget == b.HasMoveTarget
        && a.MoveTargetX == b.MoveTargetX
        && a.MoveTargetY == b.MoveTargetY
        && a.WalletBalance == b.WalletBalance
        && a.SavingsBalance == b.SavingsBalance
        && a.UnmetDemand == b.UnmetDemand
        && a.HouseholdNetworkId == b.HouseholdNetworkId
        && a.HousingUnitNetworkId == b.HousingUnitNetworkId
        && a.HousingBuildingNetworkId == b.HousingBuildingNetworkId
        && a.WorkplaceBuildingNetworkId == b.WorkplaceBuildingNetworkId
        && string.Equals(a.HousingUnitNumber, b.HousingUnitNumber, StringComparison.Ordinal)
        && string.Equals(a.JobPosition, b.JobPosition, StringComparison.Ordinal)
        && string.Equals(a.Tenure, b.Tenure, StringComparison.Ordinal)
        && string.Equals(a.HousingTier, b.HousingTier, StringComparison.Ordinal)
        && a.Homeless == b.Homeless
        && string.Equals(a.Stage, b.Stage, StringComparison.Ordinal)
        && a.Health == b.Health
        && string.Equals(a.TopSkill, b.TopSkill, StringComparison.Ordinal)
        && string.Equals(a.Seniority, b.Seniority, StringComparison.Ordinal)
        && a.IsDead == b.IsDead
        && Equals(a.Appearance, b.Appearance);

    /// <summary>
    /// Full structural comparison of every <see cref="CompanyStatus"/> field. A field omitted
    /// here never produces a delta, so clients silently hold stale values - the same failure
    /// mode <see cref="GenosesEqual"/> had. CompanyStatusEqualityTests reflects over the DTO and
    /// fails naming any field that does not affect equality.
    /// </summary>
    public static bool CompaniesEqual(CompanyStatus a, CompanyStatus b) =>
        a.NetworkId == b.NetworkId
        && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
        && a.EmployeeCount == b.EmployeeCount
        && a.OfficeCount == b.OfficeCount
        && a.Balance == b.Balance
        && a.ReservedForPayroll == b.ReservedForPayroll
        && a.Arrears == b.Arrears
        && a.RevenueLastCycle == b.RevenueLastCycle
        && a.WagesLastCycle == b.WagesLastCycle
        && string.Equals(a.Solvency, b.Solvency, StringComparison.Ordinal);

    private static Dictionary<ulong, T> IndexById<T>(List<T> items, Func<T, ulong> idSelector)
    {
        var index = new Dictionary<ulong, T>(items.Count);
        foreach (var item in items)
            index[idSelector(item)] = item;
        return index;
    }
}
